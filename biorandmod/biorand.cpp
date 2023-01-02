#define _CRT_SECURE_NO_WARNINGS
#define WIN32_LEAN_AND_MEAN

#include <algorithm>
#include <cstdint>
#include <stdio.h>
#include <windows.h>

static WCHAR _dataPath[4096];
static FILETIME _lastDataTimestamp;

static bool ReadFileData(HANDLE hFile, void* address, size_t size)
{
    DWORD dwRead;
    return ReadFile(hFile, address, size, &dwRead, NULL) && dwRead == size;
}

static void ProcessDataFile(const wchar_t* path)
{
    auto hFile = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_DELETE, NULL, OPEN_EXISTING, 0, NULL);
    if (hFile == INVALID_HANDLE_VALUE)
        return;

    FILETIME timestamp;
    if (!GetFileTime(hFile, NULL, NULL, &timestamp)) {
        CloseHandle(hFile);
        return;
    }

    if (CompareFileTime(&timestamp, &_lastDataTimestamp) < 1) {
        CloseHandle(hFile);
        return;
    }

    _lastDataTimestamp = timestamp;
    while (true)
    {
        uint32_t address;
        uint32_t size;
        if (!ReadFileData(hFile, &address, sizeof(address)))
            break;
        if (!ReadFileData(hFile, &size, sizeof(size)))
            break;
        if (!ReadFileData(hFile, (void*)address, size))
            break;
    }
    CloseHandle(hFile);
}

static void SetupDataPath(HMODULE hExecutable)
{
    DWORD pathLen = GetModuleFileNameW(hExecutable, _dataPath, std::size(_dataPath));
    for (int i = (int)pathLen - 1; i >= 0; i--)
    {
        if (_dataPath[i] == L'\\' || _dataPath[i] == L'/')
        {
            _dataPath[i + 1] = L'\0';
            break;
        }
    }
    wcsncat(_dataPath, L"mod_biorand\\biorand.dat", sizeof(_dataPath) - wcslen(_dataPath));
}

static DWORD WINAPI ModThreadRunner(LPVOID)
{
    while (true)
    {
        Sleep(5000);
        ProcessDataFile(_dataPath);
    }
    return 0;
}

static void ModMain(HMODULE hExecutable)
{
    SetupDataPath(hExecutable);
    ProcessDataFile(_dataPath);
    CreateThread(NULL, 0, &ModThreadRunner, NULL, 0, NULL);
}

BOOL WINAPI DllMain(HMODULE hModule, DWORD dwReason, LPVOID lpReserved)
{
    switch (dwReason)
    {
    case DLL_PROCESS_ATTACH:
        ModMain(GetModuleHandleA(NULL));
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}
