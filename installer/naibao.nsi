Unicode true

!include "MUI2.nsh"

!define APP_NAME "naibao"
!define APP_DISPLAY "naibao 桌面宠物"
!define APP_VERSION "1.1.2"
!define APP_PUBLISHER "naibao"
!define APP_EXE "naibao.exe"

Name "${APP_DISPLAY}"
OutFile "..\publish\naibao-setup-${APP_VERSION}.exe"
InstallDir "$LOCALAPPDATA\Programs\naibao"
InstallDirRegKey HKCU "Software\naibao" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
XPStyle on

!define MUI_ICON "..\assets\naibao.ico"
!define MUI_UNICON "..\assets\naibao.ico"
!define MUI_ABORTWARNING

; 安装完成后可直接启动宠物。
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "运行 naibao"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "SimpChinese"

Section "主程序（必装）" SecMain
  SectionIn RO
  SetOutPath "$INSTDIR"
  File /r "..\publish\win-x64\*.*"

  WriteRegStr HKCU "Software\naibao" "InstallDir" "$INSTDIR"
  WriteUninstaller "$INSTDIR\uninstall.exe"

  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\naibao" "DisplayName" "${APP_DISPLAY}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\naibao" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\naibao" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\naibao" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\naibao" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\naibao" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\naibao" "NoRepair" 1
SectionEnd

Section "开始菜单快捷方式" SecStartMenu
  CreateDirectory "$SMPROGRAMS\${APP_DISPLAY}"
  CreateShortCut "$SMPROGRAMS\${APP_DISPLAY}\naibao.lnk" "$INSTDIR\${APP_EXE}"
  CreateShortCut "$SMPROGRAMS\${APP_DISPLAY}\卸载 naibao.lnk" "$INSTDIR\uninstall.exe"
SectionEnd

Section /o "桌面快捷方式" SecDesktop
  CreateShortCut "$DESKTOP\naibao.lnk" "$INSTDIR\${APP_EXE}"
SectionEnd

Section "Uninstall"
  ; 结束正在运行的宠物进程，并清理开机自启动项。
  nsExec::ExecToLog 'taskkill /F /IM naibao.exe'
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "naibao"

  Delete "$SMPROGRAMS\${APP_DISPLAY}\naibao.lnk"
  Delete "$SMPROGRAMS\${APP_DISPLAY}\卸载 naibao.lnk"
  RMDir "$SMPROGRAMS\${APP_DISPLAY}"
  Delete "$DESKTOP\naibao.lnk"

  Delete "$INSTDIR\${APP_EXE}"
  Delete "$INSTDIR\naibao.pdb"
  Delete "$INSTDIR\uninstall.exe"
  RMDir "$INSTDIR"

  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\naibao"
  DeleteRegKey HKCU "Software\naibao"
SectionEnd
