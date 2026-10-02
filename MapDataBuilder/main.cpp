// Must precede <windows.h> to have any effect.
#define WIN32_LEAN_AND_MEAN

#include <algorithm>
#include <iostream>
#include <new>
#include <string>
#include <map>
#include <future>
#include <thread>
#include <filesystem>
#include <fstream>
#include <windows.h>

namespace CA
{
    // The {size, capacity, pointer} layout most games use. It is only the fallback for a game whose
    // CALibs exports no String constructor; see GameString.
    struct String
    {
    public:
        String()
        {
            pData = nullptr;
            size = 0;
            capacity = 0;
        }

        ~String()
        {
            size = 0;
            capacity = 0;

            if (pData != NULL)
            {
                free(pData);
                pData = NULL;
            }
        }

        String(const char* str)
        {
            pData           = _strdup(str);
            size            = static_cast<int>(strlen(pData));
            capacity        = static_cast<int>(strlen(pData));
        }

        String(String const& other)
            : size(other.size)
            , capacity(other.capacity)
        {
            // other.pData is null after a move, and _strdup(nullptr) is undefined.
            pData           = other.pData != nullptr ? _strdup(other.pData) : nullptr;
        }

        String(String && other)
        {
            pData           = std::move(other.pData);
            size            = std::move(other.size);
            capacity        = std::move(other.capacity);

            other.pData     = nullptr;
            other.size      = 0;
            other.capacity  = 0;
        }

        String& operator=(String const& other)
        {
            if (&other == this)
            {
                return *this;
            }

            // The buffer being replaced has to be released, and a moved-from source has none.
            free(pData);
            pData           = other.pData != nullptr ? _strdup(other.pData) : nullptr;
            size            = other.size;
            capacity        = other.capacity;

            return *this;
        }

        String& operator=(String && other) noexcept
        {
            if (&other == this)
            {
                return *this;
            }

            free(pData);
            pData           = other.pData;
            size            = other.size;
            capacity        = other.capacity;

            other.pData     = nullptr;
            other.size      = 0;
            other.capacity  = 0;

            return *this;
        }

        const char* raw() const
        {
            return pData;
        }

    private:
        int size;
        int capacity;
        char* pData;
    };
}

namespace CA_STD
{
    template <typename T>
    class VECTOR
    {
    public:
        VECTOR()
            : m_capacity(0)
            , m_size(0)
            , m_data(nullptr)
        {
        }

        ~VECTOR()
        {
            delete[] m_data;

            m_capacity = 0;
            m_size = 0;
            m_data = nullptr;
        }

        // The layout is handed across an ABI boundary; copying one would double-free its buffer.
        VECTOR(VECTOR const&) = delete;
        VECTOR& operator=(VECTOR const&) = delete;

        void push_back(T const& item)
        {
            int new_size = m_size + 1;
            if (new_size > m_capacity)
            {
                m_capacity = new_size * 2;
                T* new_data = new T[m_capacity];
                std::copy(m_data, m_data + m_size, new_data);
                delete[] m_data;
                m_data = new_data;
            }

            m_data[m_size++] = item;
        }

        T& get(int i)
        {
            return m_data[i];
        }

    private:
        int m_capacity;
        int m_size;
        T*  m_data;
    };
}

#ifdef _WIN64
typedef void* (*StringConstructor)(void* self, const char* value);
typedef void (*StringDestructor)(void* self);
const char* const StringConstructorSymbol = "??0String@CA@@QEAA@PEBD@Z";
const char* const StringDestructorSymbol  = "??1String@CA@@QEAA@XZ";
#else
// The exports are __thiscall: this in ECX, the rest on the stack, popped by the callee. A pointer to
// a free function cannot be __thiscall, but __fastcall with an unused EDX parameter passes and pops
// the same way.
typedef void* (__fastcall *StringConstructor)(void* self, void* unused, const char* value);
typedef void (__fastcall *StringDestructor)(void* self, void* unused);
const char* const StringConstructorSymbol = "??0String@CA@@QAE@PBD@Z";
const char* const StringDestructorSymbol  = "??1String@CA@@QAE@XZ";
#endif

StringConstructor g_constructString = nullptr;
StringDestructor  g_destroyString   = nullptr;

/// Finds the String constructor and destructor in the CALibs DLL the data builder has loaded.
/// Its file name differs between games.
void FindGameStringExports()
{
    for (const char* calibsName : { "CALibs.modder.x64.dll", "CALibs.AssemblyKit.x64.dll", "CALibs.AssemblyKit.dll" })
    {
        HMODULE calibs = ::GetModuleHandleA(calibsName);
        if (calibs == NULL)
        {
            continue;
        }

        g_constructString = (StringConstructor)::GetProcAddress(calibs, StringConstructorSymbol);
        g_destroyString   = (StringDestructor)::GetProcAddress(calibs, StringDestructorSymbol);

        if (g_constructString != nullptr && g_destroyString != nullptr)
        {
            return;
        }
    }

    g_constructString = nullptr;
    g_destroyString   = nullptr;
    std::cout << "The game's CALibs exports no CA::String constructor; passing strings in the common layout." << std::endl;
}

/// A CA::String argument for the data builder, in the layout the game itself uses. That layout is
/// not the same in every game: Pharaoh keeps a string of up to 11 characters inline, so a hand-built
/// CA::String hands it garbage for a short map name such as phar_main, and the export then quietly
/// processes no map at all. The game's own exported constructor always gets it right.
class GameString
{
public:
    explicit GameString(const char* value)
    {
        if (g_constructString != nullptr)
        {
#ifdef _WIN64
            g_constructString(m_storage, value);
#else
            g_constructString(m_storage, nullptr, value);
#endif
        }
        else
        {
            new (m_storage) CA::String(value);
        }
    }

    ~GameString()
    {
        if (g_destroyString != nullptr)
        {
#ifdef _WIN64
            g_destroyString(m_storage);
#else
            g_destroyString(m_storage, nullptr);
#endif
        }
        else
        {
            reinterpret_cast<CA::String*>(m_storage)->~String();
        }
    }

    GameString(GameString const&) = delete;
    GameString& operator=(GameString const&) = delete;

    CA::String const& get() const
    {
        return *reinterpret_cast<CA::String const*>(m_storage);
    }

private:
    // Every game's CA::String is 12 (x86) or 16 (x64) bytes.
    alignas(16) unsigned char m_storage[32];
};

namespace TOOLDATABUILDER
{
    typedef bool(__cdecl *do_campaign_maps_regions_process)(struct CA::String const&, struct CA::String const&, struct CA::String const&, struct CA::String const&);
    typedef bool(__cdecl *do_dynamic_resources_process)(struct CA::String const&, struct CA::String const&, struct CA::String const&, struct CA::String const&, struct CA::String const&, struct CA::String const&);
    typedef bool(__cdecl *process_trees)(struct CA::String const&, struct CA::String const&, CA_STD::VECTOR<CA::String> const&, struct CA::String const&, struct CA::String const&, struct CA::String const&, struct CA::String const&, struct CA::String const&, struct CA::String const&);
}

// Bit flags rather than sequential values: map_data and dyn_res are processed independently
// (see main()) and either one, or both, can be requested in a single run. Flags let a failure
// from each be reported without one masking the other when both are requested together.
enum class ReturnCodes : int
{
    Success                   = 0,
    MissingFuncName           = 1 << 0,
    LoadLibraryFailed         = 1 << 1,
    ExportMapDataFuncNotFound = 1 << 2,
    ProcessTreesFuncNotFound  = 1 << 3,
    DynResourcesFuncNotFound  = 1 << 4,
    ExportMapDataFuncFailed   = 1 << 5,
    ExportDynResFuncFailed    = 1 << 6,
    FreeLibraryFailed         = 1 << 7,
};

/// Whether this tool knows which data-builder DLL a game uses.
bool IsKnownGame(const char* gameName)
{
    return _stricmp(gameName, "rome2") == 0
        || _stricmp(gameName, "attila") == 0
        || _stricmp(gameName, "thrones") == 0
        || _stricmp(gameName, "warhammer") == 0
        || _stricmp(gameName, "three_kingdoms") == 0
        || _stricmp(gameName, "troy") == 0
        || _stricmp(gameName, "phar") == 0
        || _stricmp(gameName, "warhammer2") == 0
        || _stricmp(gameName, "warhammer3") == 0;
}

HMODULE LoadToolDataBuilderLib(const char* gameName)
{
    const char* szDataBuilderDllName = NULL;

    if (_stricmp(gameName, "rome2") == 0 ||
        _stricmp(gameName, "attila") == 0 ||
        _stricmp(gameName, "thrones") == 0)
    {
        szDataBuilderDllName = "ToolDataBuilderDll.AssemblyKit.dll";
    }
    else
    if (_stricmp(gameName, "warhammer") == 0)
    {
        szDataBuilderDllName = "ToolDataBuilderDll.AssemblyKit.x64.dll";
    }
    else
    if (_stricmp(gameName, "three_kingdoms") == 0)
    {
        szDataBuilderDllName = "ToolDataBuilder.modder.x64.dll";
    }
    else
    if (_stricmp(gameName, "troy") == 0 ||
        _stricmp(gameName, "phar") == 0 ||
        _stricmp(gameName, "warhammer2") == 0 ||
        _stricmp(gameName, "warhammer3") == 0)
    {
        szDataBuilderDllName = "ToolDataBuilderDLL.modder.x64.dll";
    }

    if (szDataBuilderDllName == NULL)
    {
        std::cout << "Unrecognised game name '" << gameName << "'." << std::endl;
        return NULL;
    }

    return ::LoadLibraryA(szDataBuilderDllName);
}

ReturnCodes DynResExport(HMODULE hToolDataBuilderDll, const char* szDesignDataPath, const char* szWorkingDataPath, const char* szMapName, const char* gameName)
{
    const char* szProcessDynResSymbol = NULL;

    if (_stricmp(gameName, "rome2") == 0 ||
        _stricmp(gameName, "attila") == 0 ||
        _stricmp(gameName, "thrones") == 0)
    {
        szProcessDynResSymbol = "?do_dynamic_resources_process@TOOLDATABUILDER@@YA_NABUString@CA@@00000@Z";
    }
    else
    if (_stricmp(gameName, "warhammer") == 0 ||
        _stricmp(gameName, "warhammer2") == 0 ||
        _stricmp(gameName, "phar") == 0 ||
        _stricmp(gameName, "troy") == 0)
    {
        szProcessDynResSymbol = "?do_dynamic_resources_process@TOOLDATABUILDER@@YA_NAEBUString@CA@@00000@Z";
    }
    else
    if (_stricmp(gameName, "warhammer3") == 0 ||
        _stricmp(gameName, "three_kingdoms") == 0)
    {
        szProcessDynResSymbol = "?do_dynamic_resources_process@TOOLDATABUILDER@@YA_NAEBVString@CA@@00000@Z";
    }

    if (szProcessDynResSymbol == NULL)
    {
        return ReturnCodes::MissingFuncName;
    }

    auto do_dynamic_resources_process_fn = (TOOLDATABUILDER::do_dynamic_resources_process)::GetProcAddress(hToolDataBuilderDll, szProcessDynResSymbol);
    if (do_dynamic_resources_process_fn == NULL)
    {
        return ReturnCodes::DynResourcesFuncNotFound;
    }

    GameString pMapName(szMapName);
    GameString pDesignDataPath(szDesignDataPath);

    std::string dynResEsfPath = std::string(szWorkingDataPath) + "/campaign_maps/" + std::string(szMapName) + "/dynamic_resources.esf";
    std::string dynResPngPath = std::string(szDesignDataPath) + "/campaign_maps/" + std::string(szMapName) + "/dynamic_resources.png";
    std::string dynResXmlPath = std::string(szDesignDataPath) + "/campaign_maps/" + std::string(szMapName) + "/dynamic_resources_database.xml";
    std::string mapHexPath = std::string(szDesignDataPath) + "/campaign_maps/" + std::string(szMapName) + "/map.hex";

    GameString pDynResEsfFile(dynResEsfPath.c_str());
    GameString pDynResPngFile(dynResPngPath.c_str());
    GameString pDynResXmlFile(dynResXmlPath.c_str());
    GameString pMapHexFile(mapHexPath.c_str());

    bool isDynResourcesProcessSuccess = do_dynamic_resources_process_fn(
        pMapName.get(), pDynResPngFile.get(), pDynResXmlFile.get(), pMapHexFile.get(), pDesignDataPath.get(), pDynResEsfFile.get());
    if (isDynResourcesProcessSuccess == false)
    {
        return ReturnCodes::ExportDynResFuncFailed;
    }

    return ReturnCodes::Success;
}

ReturnCodes MapDataExport(HMODULE hToolDataBuilderDll, const char* szDesignDataPath, const char* szWorkingDataPath, const char* szMapName, const char* szDbPath, const char* gameName)
{
    const char* szProcessMapDataSymbol  = NULL;

    if (_stricmp(gameName, "rome2") == 0 ||
        _stricmp(gameName, "attila") == 0 ||
        _stricmp(gameName, "thrones") == 0)
    {
        szProcessMapDataSymbol = "?do_campaign_maps_regions_process@TOOLDATABUILDER@@YA_NABUString@CA@@000@Z";
    }
    else
    if (_stricmp(gameName, "warhammer") == 0 ||
        _stricmp(gameName, "warhammer2") == 0 ||
        _stricmp(gameName, "troy") == 0 ||
        _stricmp(gameName, "phar") == 0)
    {
        szProcessMapDataSymbol = "?do_campaign_maps_regions_process@TOOLDATABUILDER@@YA_NAEBUString@CA@@000@Z";
    }
    else
    if (_stricmp(gameName, "warhammer3") == 0 ||
        _stricmp(gameName, "three_kingdoms") == 0)
    {
        szProcessMapDataSymbol = "?do_campaign_maps_regions_process@TOOLDATABUILDER@@YA_NAEBVString@CA@@000@Z";
    }

    if (szProcessMapDataSymbol == NULL)
    {
        return ReturnCodes::MissingFuncName;
    }

    auto do_campaign_maps_regions_process_fn = (TOOLDATABUILDER::do_campaign_maps_regions_process)::GetProcAddress(hToolDataBuilderDll, szProcessMapDataSymbol);
    if (do_campaign_maps_regions_process_fn == NULL)
    {
        return ReturnCodes::ExportMapDataFuncNotFound;
    }

    GameString pMapName(szMapName);
    GameString pDbPath(szDbPath);
    GameString pDesignDataPath(szDesignDataPath);
    GameString pWorkDataPath(szWorkingDataPath);

    bool isMapDataProcessSuccess = do_campaign_maps_regions_process_fn(pMapName.get(), pDbPath.get(), pDesignDataPath.get(), pWorkDataPath.get());
    if (isMapDataProcessSuccess == false)
    {
        return ReturnCodes::ExportMapDataFuncFailed;
    }

    return ReturnCodes::Success;
}

void CreateEmptyMapDataFile(const char* szWorkingDataPath, const char* szMapName)
{
    std::string campaignMapsPath = std::string(szWorkingDataPath) + std::string("/campaign_maps/");
    std::string campaignMapPath = std::string(szWorkingDataPath) + std::string("/campaign_maps/") + std::string(szMapName);
    std::string mapDataEsfFilePath = campaignMapPath + std::string("/map_data.esf");

    std::error_code ec;

    // create_directories, not create_directory: the latter fails if a parent is missing.
    std::filesystem::create_directories(campaignMapPath, ec);
    if (ec)
    {
        std::cout << "Failed to create '" << campaignMapPath << "': " << ec.message() << std::endl;
        return;
    }

    // The old test appended '/' to a *file* path, which never matches, so this never ran.
    std::filesystem::remove(mapDataEsfFilePath, ec);
    if (ec)
    {
        std::cout << "Failed to remove '" << mapDataEsfFilePath << "': " << ec.message() << std::endl;
        return;
    }

    std::ofstream output(mapDataEsfFilePath);
    if (!output)
    {
        std::cout << "Failed to create '" << mapDataEsfFilePath << "'" << std::endl;
    }
}

std::vector<std::string> SplitString(std::string const& input)
{
    std::string next;
    std::vector<std::string> result;

    // For each character in the string
    for (std::string::const_iterator it = input.begin(); it != input.end(); it++)
    {
        // If we've hit the terminal character
        if (*it == ',')
        {
            // If we have some characters accumulated
            if (!next.empty())
            {
                // Add them to the result vector
                result.push_back(next);
                next.clear();
            }
        }
        else
        {
            // Accumulate the next character into the sequence
            next += *it;
        }
    }

    if (!next.empty())
    {
        result.push_back(next);
    }

    return result;
}

int main(int argc, char* argv[])
{
    if (argc < 2)
    {
        return 1;
    }

    std::map<std::string, std::string> argsMap;

    for (int i = 1; i < argc; ++i)
    {
        std::string arg             = argv[i];
        size_t separatorIndex       = arg.find_first_of('=');
        std::string token           = arg.substr(0, separatorIndex);
        std::string value           = arg.substr((size_t)(separatorIndex + 1), arg.length() - separatorIndex);

        argsMap.insert({ token, value });
    }

    const char* gameName            = argsMap["game"].c_str();
    const char* asskitPath          = argsMap["akit_path"].c_str();
    const char* mapName             = argsMap["campaign_map"].c_str();
    const char* process             = argsMap["process"].c_str();

    std::string binPath             = std::string(asskitPath) + std::string("/binaries");
    std::string rawDataPath         = std::string(asskitPath) + std::string("/raw_data");
    std::string workDataPath        = std::string(asskitPath) + std::string("/working_data");
    std::string designDataPath      = std::string(rawDataPath) + std::string("/EmpireDesignData");
    std::string dbPath              = std::string(rawDataPath) + std::string("/db");

    // Without these, LoadLibraryA below resolves the DLL from somewhere else entirely.
    if (SetCurrentDirectoryA(binPath.c_str()) == 0 || SetDllDirectoryA(binPath.c_str()) == 0)
    {
        std::cout << "Failed to switch to the Assembly Kit binaries folder '" << binPath
                  << "' (error " << ::GetLastError() << "). Check the akit_path argument." << std::endl;
        return (int)ReturnCodes::LoadLibraryFailed;
    }

    std::vector<std::string> processes = SplitString(process);

    int mapDataExportResult = 0;
    int dynResExportResult = 0;

    // An unrecognised game name is a different failure from a DLL that would not load.
    if (!IsKnownGame(gameName))
    {
        return (int)ReturnCodes::MissingFuncName;
    }

    HMODULE hToolDataBuilderDll = LoadToolDataBuilderLib(gameName);
    if (hToolDataBuilderDll == NULL)
    {
        return (int)ReturnCodes::LoadLibraryFailed;
    }

    FindGameStringExports();

    if (std::find(processes.begin(), processes.end(), "map_data") != processes.end())
    {
        if (_stricmp(gameName, "rome2") == 0 ||
            _stricmp(gameName, "attila") == 0 ||
            _stricmp(gameName, "thrones") == 0)
        {
            CreateEmptyMapDataFile(workDataPath.c_str(), mapName);
        }

        mapDataExportResult = (int)MapDataExport(hToolDataBuilderDll, designDataPath.c_str(), workDataPath.c_str(), mapName, dbPath.c_str(), gameName);
    }

    if (std::find(processes.begin(), processes.end(), "dyn_res") != processes.end())
    {
        dynResExportResult = (int)DynResExport(hToolDataBuilderDll, designDataPath.c_str(), workDataPath.c_str(), mapName, gameName);
    }

    if (::FreeLibrary(hToolDataBuilderDll) == FALSE)
    {
        return (int)ReturnCodes::FreeLibraryFailed;
    }

    // Bitwise OR (not ||/priority): map_data and dyn_res run independently, and either can be
    // requested alone or both together, so a failure from one must not hide a failure from the
    // other. Whichever wasn't requested stays 0 (Success) and contributes nothing to the result.
    return mapDataExportResult | dynResExportResult;
}
