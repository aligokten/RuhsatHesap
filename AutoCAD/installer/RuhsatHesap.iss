; Ruhsat Hesap AutoCAD eklentisi kurulum betiği (Inno Setup 6).
;
; Eklenti bir "ApplicationPlugins bundle"ıdır: AutoCAD, kullanıcının
; %APPDATA%\Autodesk\ApplicationPlugins klasöründeki *.bundle klasörlerini her
; açılışta kendiliğinden yükler. Kurulumun bütün işi o klasörü yerine koymaktır;
; yönetici yetkisi gerekmez ve AutoCAD'in kendi dosyalarına dokunulmaz.
;
; Derleme (CI bunu yapar):
;   ISCC /DAppVersion=0.7.0 /DBundleDir=<...>\RuhsatHesap.bundle /DOutputDir=<...> RuhsatHesap.iss

#ifndef AppVersion
  #define AppVersion "0.7.0"
#endif
#ifndef BundleDir
  #define BundleDir "..\..\artifacts\RuhsatHesap.bundle"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\artifacts\installer"
#endif

[Setup]
AppId={{3B1F4A62-9D74-4F0E-9E3B-7A2C4D915E11}
AppName=Ruhsat Hesap AutoCAD Eklentisi
AppVersion={#AppVersion}
AppVerName=Ruhsat Hesap AutoCAD Eklentisi {#AppVersion}
AppPublisher=Ali Gökten
DefaultDirName={userappdata}\Autodesk\ApplicationPlugins\RuhsatHesap.bundle
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=RuhsatHesap-AutoCAD-Kurulum-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
UninstallDisplayName=Ruhsat Hesap AutoCAD Eklentisi
; Kaldırma dosyaları bundle klasörünün dışında dursun; AutoCAD o klasörü tarar.
UninstallFilesDir={userappdata}\RuhsatHesap\Kaldir

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Messages]
turkish.WelcomeLabel2=Bu sihirbaz [name/ver] sürümünü AutoCAD kullanıcı eklentileri klasörüne kuracak.%n%nKurulum yönetici yetkisi gerektirmez. Devam etmeden önce AutoCAD'i kapatın.
turkish.FinishedLabelNoIcons=Kurulum tamamlandı.%n%nAutoCAD'i açın: üst şeritte "Ruhsat Hesap" sekmesi görünecek. Formu açmak için RHPANEL, komut listesi için RHYARDIM yazabilirsiniz.

[InstallDelete]
; Önceki sürümden kalan DLL'ler yenileriyle karışmasın.
Type: filesandordirs; Name: "{app}\Contents"

[Files]
Source: "{#BundleDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
// AutoCAD açıkken eklenti DLL'leri kilitlidir; üzerine yazılamaz ve kurulum
// yarım kalır. Bu yüzden kurulum da kaldırma da AutoCAD kapanana kadar bekler.
function IsProcessRunning(const FileName: string): Boolean;
var
  Locator, Service, Processes: Variant;
begin
  Result := False;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('', 'root\CIMV2', '', '');
    Processes := Service.ExecQuery('SELECT Name FROM Win32_Process WHERE Name="' + FileName + '"');
    Result := (Processes.Count > 0);
  except
    // WMI kullanılamıyorsa denetim atlanır; kurulum yine de denenir.
  end;
end;

function AutoCadClosed(): Boolean;
begin
  Result := True;
  while IsProcessRunning('acad.exe') do
  begin
    if MsgBox('AutoCAD şu anda açık.' + #13#10 + #13#10 +
              'Eklenti dosyaları AutoCAD açıkken değiştirilemez. ' +
              'Lütfen AutoCAD''i tamamen kapatın ve "Yeniden Dene"ye basın.',
              mbError, MB_RETRYCANCEL) = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := AutoCadClosed();
end;

function InitializeUninstall(): Boolean;
begin
  Result := AutoCadClosed();
end;
