// Runs a Total War Assembly Kit data-builder DLL (ToolDataBuilder*.dll) the way MapDataBuilder does,
// logging every file it opens or probes. Built twice, as probe64.exe and probe32.exe (Rome II,
// Attila and Thrones ship a 32-bit DLL); tooldatabuilder.py builds and drives it.
//
//   probe trace-map-data <binaries> <dll> <export> <map> <db> <design_data> <working_data> <log> <precreate_esf 0|1>
//   probe trace-dyn-res  <binaries> <dll> <export> <map> <design_data> <working_data> <log>
//   probe string-layout  <binaries> <calibs dll>
//
// Files are logged by hooking ntdll's NtCreateFile, NtOpenFile, NtQueryAttributesFile and
// NtQueryFullAttributesFile, beneath every CRT, Win32, Qt and xerces file API, as
// "<op>\t<access mask>\t<NTSTATUS>\t<path>". The call itself sits between "#### call" and
// "#### done" lines.
//
// The probe always reruns itself on a private desktop: several data builders start a TCPConsole
// helper with a console window of its own, and the child inherits the desktop, so no window ever
// reaches the user's.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winternl.h>
#include <cstdio>
#include <cstring>
#include <string>

static HANDLE g_log = INVALID_HANDLE_VALUE;
static SRWLOCK g_lock = SRWLOCK_INIT;
static thread_local bool t_inHook;

typedef NTSTATUS(NTAPI* NtCreateFile_t)(PHANDLE, ACCESS_MASK, POBJECT_ATTRIBUTES, PIO_STATUS_BLOCK, PLARGE_INTEGER, ULONG, ULONG, ULONG, ULONG, PVOID, ULONG);
typedef NTSTATUS(NTAPI* NtOpenFile_t)(PHANDLE, ACCESS_MASK, POBJECT_ATTRIBUTES, PIO_STATUS_BLOCK, ULONG, ULONG);
typedef NTSTATUS(NTAPI* NtQueryAttributesFile_t)(POBJECT_ATTRIBUTES, PVOID);

static NtCreateFile_t          o_NtCreateFile;
static NtOpenFile_t            o_NtOpenFile;
static NtQueryAttributesFile_t o_NtQueryAttributesFile;
static NtQueryAttributesFile_t o_NtQueryFullAttributesFile;

static void Log(const char* op, POBJECT_ATTRIBUTES oa, ACCESS_MASK access, NTSTATUS status)
{
    if (t_inHook || oa == nullptr || oa->ObjectName == nullptr) return;
    t_inHook = true;
    char line[2048];
    int length = _snprintf_s(line, sizeof(line), _TRUNCATE, "%s\t%08lx\t%08lx\t%s%.*S\r\n", op, (unsigned long)access,
        (unsigned long)status, oa->RootDirectory ? "<relative>" : "", (int)(oa->ObjectName->Length / 2), oa->ObjectName->Buffer);
    if (length < 0) length = (int)strlen(line);
    AcquireSRWLockExclusive(&g_lock);
    DWORD written;
    WriteFile(g_log, line, (DWORD)length, &written, nullptr);
    ReleaseSRWLockExclusive(&g_lock);
    t_inHook = false;
}

static void Mark(const char* text)
{
    DWORD written;
    WriteFile(g_log, text, (DWORD)strlen(text), &written, nullptr);
}

static NTSTATUS NTAPI HookedNtCreateFile(PHANDLE h, ACCESS_MASK a, POBJECT_ATTRIBUTES oa, PIO_STATUS_BLOCK io, PLARGE_INTEGER al, ULONG fa, ULONG sa, ULONG cd, ULONG co, PVOID ea, ULONG el)
{
    NTSTATUS status = o_NtCreateFile(h, a, oa, io, al, fa, sa, cd, co, ea, el);
    Log("CREATE", oa, a, status);
    return status;
}

static NTSTATUS NTAPI HookedNtOpenFile(PHANDLE h, ACCESS_MASK a, POBJECT_ATTRIBUTES oa, PIO_STATUS_BLOCK io, ULONG sa, ULONG oo)
{
    NTSTATUS status = o_NtOpenFile(h, a, oa, io, sa, oo);
    Log("OPEN", oa, a, status);
    return status;
}

static NTSTATUS NTAPI HookedNtQueryAttributesFile(POBJECT_ATTRIBUTES oa, PVOID info)
{
    NTSTATUS status = o_NtQueryAttributesFile(oa, info);
    Log("ATTR", oa, 0, status);
    return status;
}

static NTSTATUS NTAPI HookedNtQueryFullAttributesFile(POBJECT_ATTRIBUTES oa, PVOID info)
{
    NTSTATUS status = o_NtQueryFullAttributesFile(oa, info);
    Log("FATTR", oa, 0, status);
    return status;
}

// Copies the syscall stub to an executable trampoline, then overwrites the stub's start with a
// jump to the hook. Both stub shapes are position-independent apart from short jumps inside the
// stub itself, which the copy keeps intact:
//   x64:   mov r10, rcx; mov eax, N; test [7FFE0308h], 1; jne; syscall; ret; int 2Eh; ret
//   WoW64: mov eax, N; mov edx, Wow64SystemServiceCall; call edx; ret n
static void* Hook(const char* name, void* hook)
{
    BYTE* target = (BYTE*)GetProcAddress(GetModuleHandleA("ntdll.dll"), name);
#ifdef _WIN64
    const bool isStub = target[0] == 0x4C && target[1] == 0x8B && target[2] == 0xD1 && target[3] == 0xB8;
    const size_t stubSize = 32, patchSize = 12;
#else
    const bool isStub = target[0] == 0xB8 && target[5] == 0xBA && target[10] == 0xFF && target[11] == 0xD2;
    const size_t stubSize = 16, patchSize = 6;
#endif
    if (!isStub)
    {
        fprintf(stderr, "%s is not a plain syscall stub on this Windows build\n", name);
        ExitProcess(90);
    }
    BYTE* trampoline = (BYTE*)VirtualAlloc(nullptr, 64, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
    memcpy(trampoline, target, stubSize);
    DWORD oldProtection;
    VirtualProtect(target, patchSize, PAGE_EXECUTE_READWRITE, &oldProtection);
#ifdef _WIN64
    target[0] = 0x48; target[1] = 0xB8;            // mov rax, hook
    memcpy(target + 2, &hook, 8);
    target[10] = 0xFF; target[11] = 0xE0;          // jmp rax
#else
    target[0] = 0x68;                              // push hook
    memcpy(target + 1, &hook, 4);
    target[5] = 0xC3;                              // ret
#endif
    VirtualProtect(target, patchSize, oldProtection, &oldProtection);
    FlushInstructionCache(GetCurrentProcess(), target, patchSize);
    return trampoline;
}

static bool StartLog(const char* path)
{
    g_log = CreateFileA(path, GENERIC_WRITE, FILE_SHARE_READ, nullptr, CREATE_ALWAYS, 0, nullptr);
    if (g_log == INVALID_HANDLE_VALUE) return false;

    o_NtCreateFile               = (NtCreateFile_t)Hook("NtCreateFile", (void*)HookedNtCreateFile);
    o_NtOpenFile                 = (NtOpenFile_t)Hook("NtOpenFile", (void*)HookedNtOpenFile);
    o_NtQueryAttributesFile      = (NtQueryAttributesFile_t)Hook("NtQueryAttributesFile", (void*)HookedNtQueryAttributesFile);
    o_NtQueryFullAttributesFile  = (NtQueryAttributesFile_t)Hook("NtQueryFullAttributesFile", (void*)HookedNtQueryFullAttributesFile);
    return true;
}

// The data builders take CA::String arguments, laid out differently by different games (Pharaoh
// keeps a string of up to 11 characters inline), so they are always built with the game's own
// exported constructor. Its CALibs DLL has a different file name in different games.
#ifdef _WIN64
typedef void* (*StringConstructor)(void* self, const char* value);
static const char* const StringConstructorSymbol = "??0String@CA@@QEAA@PEBD@Z";
#else
// __thiscall (this in ECX, the rest on the stack, callee pops) called through __fastcall with an
// unused EDX parameter, since a free function pointer cannot be __thiscall.
typedef void* (__fastcall* StringConstructor)(void* self, void* unused, const char* value);
static const char* const StringConstructorSymbol = "??0String@CA@@QAE@PBD@Z";
#endif

static StringConstructor FindStringConstructor(HMODULE calibs)
{
    if (calibs == nullptr)
    {
        for (const char* name : { "CALibs.modder.x64.dll", "CALibs.AssemblyKit.x64.dll", "CALibs.AssemblyKit.dll" })
        {
            if (calibs == nullptr) calibs = GetModuleHandleA(name);
        }
    }
    return calibs ? (StringConstructor)GetProcAddress(calibs, StringConstructorSymbol) : nullptr;
}

// Every game's CA::String is 12 (x86) or 16 (x64) bytes.
struct GameString
{
    alignas(16) unsigned char storage[32];
};

static void Construct(StringConstructor construct, GameString& target, const char* value)
{
    memset(target.storage, 0xEE, sizeof(target.storage));
#ifdef _WIN64
    construct(target.storage, value);
#else
    construct(target.storage, nullptr, value);
#endif
}

static HMODULE LoadDataBuilder(const char* binaries, const char* dll)
{
    // Without these the DLL, and the TCPConsole helper some data builders start from the current
    // directory, would be looked for somewhere else entirely.
    if (!SetCurrentDirectoryA(binaries) || !SetDllDirectoryA(binaries))
    {
        fprintf(stderr, "cannot switch to %s\n", binaries);
        return nullptr;
    }
    Mark("#### LoadLibrary\r\n");
    HMODULE module = LoadLibraryA(dll);
    if (!module) fprintf(stderr, "LoadLibrary(%s) failed %lu\n", dll, GetLastError());
    return module;
}

static int TraceMapData(char* argv[])
{
    const char* binaries = argv[2]; const char* dll = argv[3]; const char* symbol = argv[4];
    std::string map = argv[5], db = argv[6], design = argv[7], work = argv[8];
    bool precreateEsf = argv[10][0] == '1';

    // MapDataBuilder does this for Rome II, Attila and Thrones before calling their DLL.
    if (precreateEsf)
    {
        std::string esf = work + "/campaign_maps/" + map + "/map_data.esf";
        HANDLE file = CreateFileA(esf.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, 0, nullptr);
        if (file == INVALID_HANDLE_VALUE) { fprintf(stderr, "cannot create %s\n", esf.c_str()); return 7; }
        CloseHandle(file);
    }

    if (!StartLog(argv[9])) return 2;
    HMODULE module = LoadDataBuilder(binaries, dll);
    if (!module) return 4;

    typedef bool(__cdecl* MapDataProcess)(GameString const&, GameString const&, GameString const&, GameString const&);
    auto process = (MapDataProcess)GetProcAddress(module, symbol);
    StringConstructor construct = FindStringConstructor(nullptr);
    if (!process || !construct) { fprintf(stderr, "export or CA::String constructor not found\n"); return 5; }

    static GameString mapName, dbPath, designPath, workPath;
    Construct(construct, mapName, map.c_str());
    Construct(construct, dbPath, db.c_str());
    Construct(construct, designPath, design.c_str());
    Construct(construct, workPath, work.c_str());

    Mark("#### call\r\n");
    bool ok = process(mapName, dbPath, designPath, workPath);
    Mark("#### done\r\n");

    printf("do_campaign_maps_regions_process returned %d\n", ok ? 1 : 0);
    fflush(stdout);
    // Skip FreeLibrary and static destructors - the result is all that matters here.
    TerminateProcess(GetCurrentProcess(), ok ? 0 : 6);
    return 0;
}

static int TraceDynRes(char* argv[])
{
    const char* binaries = argv[2]; const char* dll = argv[3]; const char* symbol = argv[4];
    std::string map = argv[5], design = argv[6], work = argv[7];

    if (!StartLog(argv[8])) return 2;
    HMODULE module = LoadDataBuilder(binaries, dll);
    if (!module) return 4;

    typedef bool(__cdecl* DynResProcess)(GameString const&, GameString const&, GameString const&, GameString const&, GameString const&, GameString const&);
    auto process = (DynResProcess)GetProcAddress(module, symbol);
    StringConstructor construct = FindStringConstructor(nullptr);
    if (!process || !construct) { fprintf(stderr, "export or CA::String constructor not found\n"); return 5; }

    // The same arguments MapDataBuilder passes.
    std::string mapFolder = design + "/campaign_maps/" + map;
    std::string values[6] = { map, mapFolder + "/dynamic_resources.png", mapFolder + "/dynamic_resources_database.xml",
                              mapFolder + "/map.hex", design, work + "/campaign_maps/" + map + "/dynamic_resources.esf" };
    static GameString arguments[6];
    for (int i = 0; i < 6; ++i) Construct(construct, arguments[i], values[i].c_str());

    Mark("#### call\r\n");
    bool ok = process(arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5]);
    Mark("#### done\r\n");

    printf("do_dynamic_resources_process returned %d\n", ok ? 1 : 0);
    fflush(stdout);
    TerminateProcess(GetCurrentProcess(), ok ? 0 : 6);
    return 0;
}

// Shows how the game's own CA::String lays out short and long strings.
static int StringLayout(char* argv[])
{
    SetCurrentDirectoryA(argv[2]);
    SetDllDirectoryA(argv[2]);
    HMODULE calibs = LoadLibraryA(argv[3]);
    if (!calibs) { fprintf(stderr, "LoadLibrary(%s) failed %lu\n", argv[3], GetLastError()); return 4; }
    StringConstructor construct = FindStringConstructor(calibs);
    if (!construct) { fprintf(stderr, "%s exports no CA::String constructor\n", argv[3]); return 5; }

    for (const char* sample : { "abc", "phar_combi", "abcdefghijk", "abcdefghijkl", "main_attila_map" })
    {
        GameString value;
        Construct(construct, value, sample);
        printf("%-16s length %2zu:", sample, strlen(sample));
        for (int i = 0; i < 24; ++i) printf(" %02x", value.storage[i]);
        void* pointerAt8;
        memcpy(&pointerAt8, value.storage + 8, sizeof(pointerAt8));
        printf("  | inline at 4: %s, pointer at 8: %s\n",
            memcmp(value.storage + 4, sample, strlen(sample)) == 0 ? "yes" : "no",
            (!IsBadReadPtr(pointerAt8, 1) && strcmp((const char*)pointerAt8, sample) == 0) ? "yes" : "no");
    }
    fflush(stdout);
    TerminateProcess(GetCurrentProcess(), 0);
    return 0;
}

static int RunOnPrivateDesktop()
{
    HDESK desktop = CreateDesktopW(L"tooldatabuilder_probe", nullptr, nullptr, 0, GENERIC_ALL, nullptr);
    if (!desktop) { fprintf(stderr, "CreateDesktop failed %lu\n", GetLastError()); return 8; }

    SetEnvironmentVariableA("TOOLDATABUILDER_PROBE_CHILD", "1");
    // Windows skips the current directory - where the TCPConsole helper is - when this is set.
    SetEnvironmentVariableA("NoDefaultCurrentDirectoryInExePath", nullptr);

    wchar_t desktopName[] = L"tooldatabuilder_probe";
    STARTUPINFOW startup = { sizeof(startup) };
    startup.lpDesktop  = desktopName;
    startup.dwFlags    = STARTF_USESTDHANDLES;
    startup.hStdInput  = GetStdHandle(STD_INPUT_HANDLE);
    startup.hStdOutput = GetStdHandle(STD_OUTPUT_HANDLE);
    startup.hStdError  = GetStdHandle(STD_ERROR_HANDLE);
    PROCESS_INFORMATION child;
    if (!CreateProcessW(nullptr, GetCommandLineW(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW, nullptr, nullptr, &startup, &child))
    {
        fprintf(stderr, "CreateProcess on the private desktop failed %lu\n", GetLastError());
        return 9;
    }
    WaitForSingleObject(child.hProcess, INFINITE);
    DWORD exitCode = 0;
    GetExitCodeProcess(child.hProcess, &exitCode);
    CloseHandle(child.hThread);
    CloseHandle(child.hProcess);
    CloseDesktop(desktop);
    return (int)exitCode;
}

int main(int argc, char* argv[])
{
    const bool traceMapData = argc == 11 && strcmp(argv[1], "trace-map-data") == 0;
    const bool traceDynRes  = argc == 9  && strcmp(argv[1], "trace-dyn-res") == 0;
    const bool stringLayout = argc == 4  && strcmp(argv[1], "string-layout") == 0;
    if (!traceMapData && !traceDynRes && !stringLayout)
    {
        fprintf(stderr,
            "usage: probe trace-map-data <binaries> <dll> <export> <map> <db> <design_data> <working_data> <log> <precreate_esf 0|1>\n"
            "       probe trace-dyn-res  <binaries> <dll> <export> <map> <design_data> <working_data> <log>\n"
            "       probe string-layout  <binaries> <calibs dll>\n");
        return 1;
    }

    if (GetEnvironmentVariableA("TOOLDATABUILDER_PROBE_CHILD", nullptr, 0) == 0)
    {
        return RunOnPrivateDesktop();
    }

    if (traceMapData) return TraceMapData(argv);
    if (traceDynRes)  return TraceDynRes(argv);
    return StringLayout(argv);
}
