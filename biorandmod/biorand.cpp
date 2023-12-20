#define _CRT_SECURE_NO_WARNINGS
#define WIN32_LEAN_AND_MEAN

#include <algorithm>
#include <cstdint>
#include <stdio.h>
#include <windows.h>

static WCHAR _dataPath[4096];
static FILETIME _lastDataTimestamp;
static void* _largeWorkArea;

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

		uint8_t dataStack[256];
		uint8_t* data = dataStack;
		if (size > sizeof(dataStack))
		{
			data = (uint8_t*)malloc(size);
		}
		if (data != nullptr)
		{
			if (ReadFileData(hFile, data, size))
			{
				WriteProcessMemory(GetCurrentProcess(), (LPVOID)address, data, size, NULL);
			}
			if (data != dataStack)
			{
				free(data);
				data = nullptr;
			}
		}
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
	wcsncat(_dataPath, L"mod_biorand\\biorand.dat", std::size(_dataPath) - wcslen(_dataPath));
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

typedef BYTE(*fnLoadGame)(LPVOID, LPCSTR, DWORD);
static fnLoadGame _originalLoadGame;

static BYTE LoadGameHookCallback(LPVOID buffer, LPCSTR filename, DWORD bytes)
{
	auto result = _originalLoadGame(buffer, filename, bytes);

	// file: 0x689A98+0x1F4
	// buffer: 0x689A98+0x1F4
	// inventory: 0x98E79C + 0x598

	auto player = (uint8_t*)((BYTE*)buffer + 0x20A);
	auto stage = (uint16_t*)((BYTE*)buffer + 0x378);
	auto room = (uint8_t*)((BYTE*)buffer + 0x37A);
	auto item0 = (BYTE*)buffer + 0x598;
	if (*stage == 0 && *room == 4)
	{
		auto AddressInventoryLeon = (BYTE*)(0x400000 + 0x001401B8);
		auto AddressInventoryClaire = (BYTE*)(0x400000 + 0x001401D9);

		auto src = *player == 0 ? AddressInventoryLeon : AddressInventoryClaire;
		auto dst = item0;
		for (int i = 0; i < 11; i++)
		{
			*dst++ = *src++;
			*dst++ = *src++;
			*dst++ = *src++;
			dst++;
		}
	}
	return result;
}

typedef void(*fnSplInit)(void*);
static void Spl_Init_2(void* spl)
{
	*((uint16_t*)((uint8_t*)spl + 0x0156)) = 200; // Health
	*((uint16_t*)((uint8_t*)spl + 0x0162)) = 200; // Max health

	auto cl = *((uint8_t*)spl + 0x0008);
	auto routine = (fnSplInit*)(0x53CAC8);
	routine[cl](spl);
}

static void RE2_HookLoadGame()
{
	BYTE b = 0;
	ReadProcessMemory(GetCurrentProcess(), (LPVOID)0x004C5E13, &b, sizeof(b), NULL);
	if (b != 0xE8)
	{
		// Not RE 2
		return;
	}

	if (ReadProcessMemory(GetCurrentProcess(), (LPVOID)(0x004C5E13 + 1), &_originalLoadGame, sizeof(_originalLoadGame), NULL))
	{
		_originalLoadGame = (fnLoadGame)(0x004C5E13 + 5 + (BYTE*)_originalLoadGame);
		auto cb = (BYTE*)&LoadGameHookCallback - 0x004C5E13 - 5;
		if (WriteProcessMemory(GetCurrentProcess(), (LPVOID)(0x004C5E13 + 1), &cb, sizeof(cb), NULL))
		{
		}
	}

	// When a stage is loaded, it sets up a work buffer that is used for room storage and operations
	// This is tiny from PSX days, but we can make it much larger to reduce chance of crashes when
	// we pack rooms full of stuff like enemies and NPCs
	_largeWorkArea = malloc(16 * 1024 * 1024);
	WriteProcessMemory(GetCurrentProcess(), (LPVOID)(0x4DEF10), &_largeWorkArea, sizeof(_largeWorkArea), NULL);
	b = 0xEB;
	WriteProcessMemory(GetCurrentProcess(), (LPVOID)(0x4AC5D1), &b, sizeof(b), NULL);
	WriteProcessMemory(GetCurrentProcess(), (LPVOID)(0x4B1AE8), &b, sizeof(b), NULL);

	auto address = (uintptr_t)&Spl_Init_2 - 0x4EF9AA - 5;
	BYTE instr[] = { 0xE8, 0x00, 0x00, 0x00, 0x00, 0x90, 0x90, 0x90, 0x90, 0x90 };
	memcpy(&instr[1], &address, sizeof(address));
	WriteProcessMemory(GetCurrentProcess(), (LPVOID)(0x4EF9AA), instr, sizeof(instr), NULL);
}

static void ModMain(HMODULE hExecutable)
{
	RE2_HookLoadGame();
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
