// Must precede <windows.h> to have any effect.
#define WIN32_LEAN_AND_MEAN

#include <algorithm>
#include <iostream>
#include <string>
#include <map>
#include <future>
#include <thread>
#include <filesystem>
#include <fstream>
#include <windows.h>

namespace CA
{
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

    CA::String pMapName(szMapName);
    CA::String pDesignDataPath(szDesignDataPath);

    std::string dynResEsfPath = std::string(szWorkingDataPath) + "/campaign_maps/" + std::string(szMapName) + "/dynamic_resources.esf";
    std::string dynResPngPath = std::string(szDesignDataPath) + "/campaign_maps/" + std::string(szMapName) + "/dynamic_resources.png";
    std::string dynResXmlPath = std::string(szDesignDataPath) + "/campaign_maps/" + std::string(szMapName) + "/dynamic_resources_database.xml";
    std::string mapHexPath = std::string(szDesignDataPath) + "/campaign_maps/" + std::string(szMapName) + "/map.hex";

    CA::String pDynResEsfFile(dynResEsfPath.c_str());
    CA::String pDynResPngFile(dynResPngPath.c_str());
    CA::String pDynResXmlFile(dynResXmlPath.c_str());
    CA::String pMapHexFile(mapHexPath.c_str());
    
    bool isDynResourcesProcessSuccess = do_dynamic_resources_process_fn(pMapName, pDynResPngFile, pDynResXmlFile, pMapHexFile, pDesignDataPath, pDynResEsfFile);
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

    std::string mapHexPath = std::string(szDesignDataPath) + "/campaign_maps/" + std::string(szMapName) + "/map.hex";

    CA::String pMapName(szMapName);
    CA::String pDbPath(szDbPath);
    CA::String pDesignDataPath(szDesignDataPath);
    CA::String pWorkDataPath(szWorkingDataPath);
    CA::String pMapHexFile(mapHexPath.c_str());
    
    bool isMapDataProcessSuccess = do_campaign_maps_regions_process_fn(pMapName, pDbPath, pDesignDataPath, pWorkDataPath);
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

    // db_path points the map_data export at tables CAIME wrote to a folder of its own (the RPFM
    // database source) rather than the Assembly Kit's.
    std::string dbPath              = argsMap.count("db_path") != 0 ? argsMap["db_path"] : std::string(rawDataPath) + std::string("/db");

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
