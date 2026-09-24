@echo off
setlocal enabledelayedexpansion
REM Prophet.Core Windows Build Script

REM 设置控制台代码页为 UTF-8，解决中文乱码问题
chcp 65001 > nul

REM 解析命令行参数
set BUILD_MODE=both
if "%1"=="--debug" set BUILD_MODE=debug
if "%1"=="--release" set BUILD_MODE=release
if "%1"=="--both" set BUILD_MODE=both
if "%1"=="/?" goto :usage
if "%1"=="--help" goto :usage

echo ========================================
echo Prophet.Core Build Script
echo ========================================
if "%BUILD_MODE%"=="debug" (
    echo [INFO] Build mode: Debug only
) else if "%BUILD_MODE%"=="release" (
    echo [INFO] Build mode: Release only
) else (
    echo [INFO] Build mode: Both Debug and Release
)
echo.

REM Check if in Prophet.Core directory
if exist "CMakeLists.txt" (
    REM Continue
) else (
    echo Error: Please run this script in Prophet.Core directory
    exit /b 1
)

REM 查找 CMake（支持多个可能路径）
set CMAKE_EXE=
if exist "D:\Program Files\CMake\bin\cmake.exe" (
    set CMAKE_EXE="D:\Program Files\CMake\bin\cmake.exe"
) else if exist "C:\Program Files\CMake\bin\cmake.exe" (
    set CMAKE_EXE="C:\Program Files\CMake\bin\cmake.exe"
) else (
    REM 尝试从 PATH 查找
    where cmake >nul 2>&1
    if errorlevel 1 (
        echo Error: CMake not found!
        echo Please install CMake from https://cmake.org/download/
        exit /b 1
    ) else (
        set CMAKE_EXE=cmake
    )
)

REM 清理父目录中可能存在的旧缓存文件
if exist "CMakeCache.txt" (
    echo [WARN] Found CMakeCache.txt in parent directory, removing...
    del /F /Q CMakeCache.txt 2>nul
)
if exist "CMakeFiles" (
    echo [WARN] Found CMakeFiles in parent directory, removing...
    rmdir /S /Q CMakeFiles 2>nul
)

REM Create and enter build directory
if not exist "build" (
    mkdir build
)
cd build

echo.
echo [1/4] Configuring CMake...

REM 自动检测可用的生成器
set GENERATOR=
set GENERATOR_ARGS=
set BUILD_CONFIG=Release

REM 通过 vswhere 查找已安装「使用 C++ 的桌面开发」工作负载的 Visual Studio
echo [INFO] Setting up MSVC compiler environment...

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo [ERROR] Visual Studio Installer not found.
    echo Please install Visual Studio with "Desktop development with C++".
    cd ..
    exit /b 1
)

set "VS_INSTALL_PATH="
set "VS_DISPLAY_NAME="
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do (
    set "VS_INSTALL_PATH=%%i"
)
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property displayName`) do (
    set "VS_DISPLAY_NAME=%%i"
)

if not defined VS_INSTALL_PATH (
    set "VS_ANY_PATH="
    for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -property installationPath`) do (
        set "VS_ANY_PATH=%%i"
    )
    echo [ERROR] MSVC C++ compiler not found!
    echo.
    if defined VS_ANY_PATH (
        echo Visual Studio is installed at:
        echo   !VS_ANY_PATH!
        echo.
        echo But the required workload is missing:
        echo   "Desktop development with C++" / "使用 C++ 的桌面开发"
        echo.
        echo Open Visual Studio Installer, click Modify, and install that workload.
    ) else (
        echo Please install Visual Studio or Build Tools with:
        echo   "Desktop development with C++" / "使用 C++ 的桌面开发"
    )
    cd ..
    exit /b 1
)

set "VCVARS=%VS_INSTALL_PATH%\VC\Auxiliary\Build\vcvars64.bat"
if not exist "%VCVARS%" (
    echo [ERROR] vcvars64.bat not found:
    echo   %VCVARS%
    echo.
    echo Please repair or reinstall "Desktop development with C++".
    cd ..
    exit /b 1
)

call "%VCVARS%"
where cl >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Failed to initialize MSVC compiler from:
    echo   %VCVARS%
    cd ..
    exit /b 1
)

if defined VS_DISPLAY_NAME (
    echo [INFO] Using MSVC - !VS_DISPLAY_NAME!
) else (
    echo [INFO] Using MSVC - %VS_INSTALL_PATH%
)
echo [INFO] vcvars64: %VCVARS%

:vs_ready

REM 验证编译器是否可用
where cl >nul 2>&1
if errorlevel 1 (
    echo [ERROR] MSVC compiler ^(cl.exe^) not found in PATH!
    echo Please run this script in "Developer Command Prompt for VS"
    cd ..
    exit /b 1
)

REM 检查是否已经配置过（如果 CMakeCache.txt 存在，检查生成器类型和路径）
echo [DEBUG] Current directory: %CD%
echo [DEBUG] Checking for CMakeCache.txt...
dir CMakeCache.txt >nul 2>&1
if %errorlevel% equ 0 (
    echo [INFO] Found existing CMake configuration, checking validity...
    
    REM 获取当前项目的父目录路径（CMake源目录）
    set "EXPECTED_SOURCE_DIR=%CD%\.."
    
    REM 检查CMakeCache.txt中的路径是否匹配
    set "PATH_MISMATCH=0"
    REM 获取当前驱动器字母（如 D:）
    set "CURRENT_DRIVE=%CD:~0,2%"
    
    REM 检查缓存中是否包含当前驱动器的路径
    findstr /C:"CMAKE_HOME_DIRECTORY:INTERNAL=" CMakeCache.txt >nul 2>&1
    if !errorlevel! equ 0 (
        REM 使用临时文件来安全地提取路径
        findstr /C:"CMAKE_HOME_DIRECTORY:INTERNAL=" CMakeCache.txt >cache_path.tmp 2>nul
        if exist cache_path.tmp (
            REM 检查是否包含当前驱动器
            findstr /C:"%CURRENT_DRIVE%" cache_path.tmp >nul 2>&1
            if !errorlevel! neq 0 (
                REM 路径不匹配（可能是不同驱动器）
                set "PATH_MISMATCH=1"
                echo [WARN] CMake cache created for different drive
            )
            del cache_path.tmp 2>nul
        )
    )
    
    REM 如果路径不匹配，强制清理
    if "!PATH_MISMATCH!"=="1" (
        echo [WARN] CMake cache path mismatch detected, clearing cache...
        del /F /Q CMakeCache.txt 2>nul
        if exist CMakeFiles (
            echo [INFO] Removing CMakeFiles directory...
            rmdir /S /Q CMakeFiles 2>nul
        )
        echo [OK] Cache cleared due to path mismatch
    ) else (
        REM 路径匹配，检查生成器类型
        findstr /C:"CMAKE_GENERATOR:INTERNAL=Ninja" CMakeCache.txt >nul 2>&1
        if %errorlevel% equ 0 (
            echo [INFO] Existing Ninja configuration found, will reuse...
        ) else (
            REM 不是Ninja生成器，需要清理
            echo [WARN] Non-Ninja generator detected, clearing cache...
            del /F /Q CMakeCache.txt 2>nul
            if exist CMakeFiles (
                echo [INFO] Removing CMakeFiles directory...
                rmdir /S /Q CMakeFiles 2>nul
            )
            echo [OK] Cache cleared successfully
        )
    )
) else (
    echo [INFO] No existing cache found, will create new configuration
)

REM Use Ninja generator (directly use MSVC compiler, no Visual Studio registration needed)
REM Determine which versions to build based on BUILD_MODE parameter

REM Build Debug version (if needed)
if "%BUILD_MODE%"=="debug" goto :build_debug
if "%BUILD_MODE%"=="both" goto :build_debug
goto :skip_debug

:build_debug
echo.
echo [INFO] ========================================
if "%BUILD_MODE%"=="both" (
    echo [INFO] Step 1/2: Building Debug version...
) else (
    echo [INFO] Building Debug version...
)
echo [INFO] ========================================
%CMAKE_EXE% .. -G "Ninja" -DCMAKE_BUILD_TYPE=Debug -DCMAKE_CXX_COMPILER=cl.exe >cmake_output.tmp 2>&1
set CMAKE_RESULT=%errorlevel%
findstr /C:"does not match" cmake_output.tmp >nul 2>&1
if %errorlevel% equ 0 (
    echo [WARN] CMake detected path mismatch, cleaning cache and retrying...
    type cmake_output.tmp
    del /F /Q CMakeCache.txt 2>nul
    if exist CMakeFiles (
        rmdir /S /Q CMakeFiles 2>nul
    )
    echo [INFO] Retrying CMake configuration...
    del cmake_output.tmp 2>nul
    %CMAKE_EXE% .. -G "Ninja" -DCMAKE_BUILD_TYPE=Debug -DCMAKE_CXX_COMPILER=cl.exe
    if errorlevel 1 (
        echo [ERROR] CMake configuration failed ^(Debug^)
        cd ..
        exit /b 1
    )
) else (
    type cmake_output.tmp
    del cmake_output.tmp 2>nul
    if !CMAKE_RESULT! neq 0 (
        echo [ERROR] CMake configuration failed ^(Debug^)
        cd ..
        exit /b 1
    )
)
echo [INFO] Configuration successful ^(Debug^)
%CMAKE_EXE% --build .
if errorlevel 1 (
    echo [ERROR] Build failed ^(Debug^)
    cd ..
    exit /b 1
)
echo [OK] Debug build complete
:skip_debug

REM Build Release version (if needed)
if "%BUILD_MODE%"=="release" goto :build_release
if "%BUILD_MODE%"=="both" goto :build_release
goto :skip_release

:build_release
echo.
echo [INFO] ========================================
if "%BUILD_MODE%"=="both" (
    echo [INFO] Step 2/2: Building Release version...
) else (
    echo [INFO] Building Release version...
)
echo [INFO] ========================================
%CMAKE_EXE% .. -G "Ninja" -DCMAKE_BUILD_TYPE=Release -DCMAKE_CXX_COMPILER=cl.exe >cmake_output.tmp 2>&1
set CMAKE_RESULT=%errorlevel%
findstr /C:"does not match" cmake_output.tmp >nul 2>&1
if %errorlevel% equ 0 (
    echo [WARN] CMake detected path mismatch, cleaning cache and retrying...
    type cmake_output.tmp
    del /F /Q CMakeCache.txt 2>nul
    if exist CMakeFiles (
        rmdir /S /Q CMakeFiles 2>nul
    )
    echo [INFO] Retrying CMake configuration...
    del cmake_output.tmp 2>nul
    %CMAKE_EXE% .. -G "Ninja" -DCMAKE_BUILD_TYPE=Release -DCMAKE_CXX_COMPILER=cl.exe
    if errorlevel 1 (
        echo [ERROR] CMake configuration failed ^(Release^)
        cd ..
        exit /b 1
    )
) else (
    type cmake_output.tmp
    del cmake_output.tmp 2>nul
    if !CMAKE_RESULT! neq 0 (
        echo [ERROR] CMake configuration failed ^(Release^)
        cd ..
        exit /b 1
    )
)
echo [INFO] Configuration successful ^(Release^)
%CMAKE_EXE% --build .
if errorlevel 1 (
    echo [ERROR] Build failed ^(Release^)
    cd ..
    exit /b 1
)
echo [OK] Release build complete
:skip_release

set BUILD_CONFIG=
goto :configured

:configure
REM 配置 CMake
REM 如果使用 Visual Studio 生成器，尝试设置环境变量
if "%GENERATOR_ARGS%"=="" (
    %CMAKE_EXE% .. -G %GENERATOR%
) else (
    if defined VCVARS if exist "%VCVARS%" (
        call "%VCVARS%" >nul 2>&1
    )
    %CMAKE_EXE% .. -G %GENERATOR% %GENERATOR_ARGS%
)
if errorlevel 1 (
    echo Error: CMake configuration failed
    cd ..
    exit /b 1
)

:configured
REM 检测生成器类型
set IS_VISUAL_STUDIO=0
findstr /C:"CMAKE_GENERATOR:INTERNAL=Visual Studio" CMakeCache.txt >nul 2>&1
if %errorlevel% equ 0 (
    set IS_VISUAL_STUDIO=1
    set BUILD_CONFIG=Release
)

REM 对于 Ninja 生成器，Release 版本已经在前面构建了
REM 这里只处理 Visual Studio 生成器的情况
if "%IS_VISUAL_STUDIO%"=="1" (
    if "%BUILD_MODE%"=="release" goto :build_vs_release
    if "%BUILD_MODE%"=="both" goto :build_vs_release
    goto :skip_vs_release
)
goto :skip_vs_release

:build_vs_release
if "%IS_VISUAL_STUDIO%"=="1" (
    echo.
    if "%BUILD_MODE%"=="both" (
        echo [2/4] Building Release version...
    ) else if "%BUILD_MODE%"=="release" (
        echo [2/4] Building Release version...
    )
    %CMAKE_EXE% --build . --config %BUILD_CONFIG%
    if errorlevel 1 (
        echo [ERROR] Build failed ^(Release^)
        cd ..
        exit /b 1
    )
    echo [OK] Release build complete
)
:skip_vs_release

echo.
echo [3/4] Checking build artifacts...

REM 检测构建输出（Ninja 生成器会根据 CMAKE_BUILD_TYPE 输出到对应子目录）
REM 检查 Debug 版本的 DLL
if exist "bin\Debug\prophet_core.dll" (
    echo [OK] Debug DLL:   build\bin\Debug\prophet_core.dll
) else (
    echo [WARN] Debug DLL not found
)

REM 检查 Release 版本的 DLL
if exist "bin\Release\prophet_core.dll" (
    echo [OK] Release DLL: build\bin\Release\prophet_core.dll
) else (
    echo [WARN] Release DLL not found
)

REM 复制 Python 模块到 Prophet.Core 根目录
REM 优先使用 Release 版本，如果没有则使用 Debug 版本
if "%BUILD_MODE%"=="release" (
    if exist "Release\Prophet.*.pyd" (
        copy /Y Release\Prophet.*.pyd ..\ 2>nul
        echo [OK] Prophet.pyd ^(Release^) copied to Prophet.Core/ root directory
    ) else (
        echo [WARN] Prophet.pyd not found in Release directory
    )
) else (
    REM Debug 或 both 模式，优先使用 Release，否则使用 Debug
    if exist "Release\Prophet.*.pyd" (
        copy /Y Release\Prophet.*.pyd ..\ 2>nul
        echo [OK] Prophet.pyd ^(Release^) copied to Prophet.Core/ root directory
    ) else if exist "Debug\Prophet.*.pyd" (
        copy /Y Debug\Prophet.*.pyd ..\ 2>nul
        echo [OK] Prophet.pyd ^(Debug^) copied to Prophet.Core/ root directory
    ) else (
        echo [WARN] Prophet.pyd not found
    )
)

cd ..

echo.
echo [4/4] Verifying installation...
python -c "import sys; sys.path.insert(0, 'Prophet.Core'); import Prophet; print('[OK] Prophet.Core module imported'); print('Version:', Prophet.__version__); engine = Prophet.Engine('ALL{1=1}=HOLD;', {}); print('[OK] Engine created successfully')" 2>&1
if errorlevel 1 (
    echo [ERROR] Import or initialization failed
    echo Please check if Prophet.Core.pyd is in Prophet.Core/ root directory
) else (
    echo [OK] Verification successful!
)

echo.
echo ========================================
echo Build Complete!
echo ========================================
echo.
echo [INFO] Python 模块 (Prophet.Core.pyd):
echo   只暴露核心引擎接口，遵循最小化原则
echo   
echo   可用接口:
echo   - Engine              [策略引擎]
echo   - BatchEngine         [批量评估引擎]
echo   - Signal, Value       [数据结构]
echo   
echo   不暴露:
echo   - 指标计算接口（内部使用）
echo   - 数据函数接口（内部使用）
echo.
echo [INFO] C API DLL (prophet_core.dll):
echo   Debug 版本:   Prophet.Core\build\bin\Debug\prophet_core.dll
echo   Release 版本: Prophet.Core\build\bin\Release\prophet_core.dll
echo   
echo   可用接口:
echo   - 策略引擎 API        [Engine 创建/评估]
echo   - 指标计算 API        [批量绘图用，约30个指标]
echo   - K线转换 API         [时间框架转换]
echo   
echo   Prophet.API 和 Prophet.Client 会在各自构建时自动复制对应的 DLL
echo.
echo [INFO] Python 使用方式:
echo   import sys
echo   sys.path.insert(0, 'Prophet.Core')
echo   import Prophet
echo   
echo   # 创建引擎（DSL 内部完成所有计算）
echo   engine = Prophet.Engine(dsl_code)
echo   engine.set_klines(...)
echo   signal = engine.get_signal(price, time)
echo.
echo [INFO] C# 使用方式:
echo   // 策略引擎
echo   var engine = ProphetCore.CreateEngine(dsl);
echo   var signal = ProphetCore.GetSignal(engine, price, time);
echo   
echo   // 绘图辅助（批量计算）
echo   var macd = ProphetCore.Prophet_MACD(close, 12, 26, 9);
echo   var klines5m = ProphetCore.ConvertKlines(klines1m, 1, 5);
echo.
echo [TIP] 运行测试:
echo   python test_prophet_core_comprehensive.py
echo.
echo [TIP] Prophet.Client 构建说明:
echo   dotnet build                # 使用 Debug 配置（默认）
echo   dotnet build -c Release     # 使用 Release 配置
echo.
if "%BUILD_MODE%"=="both" (
    echo   两个版本的 DLL 都已编译，Prophet.Client 会根据配置自动选择
) else if "%BUILD_MODE%"=="debug" (
    echo   仅编译了 Debug 版本，Prophet.Client 需要使用 Debug 配置
) else (
    echo   仅编译了 Release 版本，Prophet.Client 需要使用 Release 配置
)
echo.
echo [TIP] 编译选项:
echo   build.bat              # 编译 Debug 和 Release（默认）
echo   build.bat --debug       # 仅编译 Debug 版本（更快）
echo   build.bat --release     # 仅编译 Release 版本（更快）
echo   build.bat --both        # 显式指定编译两个版本
echo   build.bat --help        # 显示此帮助信息
echo.

pause
exit /b 0

:usage
echo.
echo 用法: build.bat [选项]
echo.
echo 选项:
echo   --debug     仅编译 Debug 版本（适用于开发调试）
echo   --release   仅编译 Release 版本（适用于发布）
echo   --both      编译 Debug 和 Release 两个版本（默认）
echo   --help, /?  显示此帮助信息
echo.
echo 示例:
echo   build.bat              # 编译两个版本
echo   build.bat --debug      # 仅编译 Debug（更快）
echo   build.bat --release    # 仅编译 Release（更快）
echo.
exit /b 0
