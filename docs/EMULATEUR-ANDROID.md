# Émulateur Android — Gaia-Life (App.Mobile)

Guide pour tester l’app MAUI Blazor Hybrid (`net9.0-android`) sur Windows **sans Visual Studio**, via un double-clic.

## Lancement quotidien

1. Double-cliquer sur [`scripts/lancer-emulateur-gaialife.bat`](../scripts/lancer-emulateur-gaialife.bat)  
   **ou** en PowerShell depuis la racine du dépôt :

```powershell
.\scripts\run-emulator.ps1
```

2. Le script :
   - vérifie / installe les prérequis SDK si possible ;
   - crée l’AVD `GaiaLifeTest` **seulement s’il n’existe pas** ;
   - démarre l’émulateur s’il n’est pas déjà lancé ;
   - **attend le boot complet** ;
   - exécute `dotnet build -t:Run -f net9.0-android` sur `src/App.Mobile`.

Relancer le script plusieurs fois est **sans danger** (idempotent) : pas de second AVD, pas d’erreur si l’émulateur tourne déjà.

Journaux locaux (non versionnés) : `scripts/.emulator-logs/`.

---

## Prérequis

### 1. .NET 9 SDK + workload MAUI

```powershell
dotnet --version
dotnet workload install maui
dotnet workload list
```

La charge `maui` (ou au minimum `maui-android`) doit apparaître. Sans elle, le build `net9.0-android` échoue.

### 2. Variables d’environnement

| Variable | Rôle | Exemple typique Windows |
| --- | --- | --- |
| `ANDROID_HOME` ou `ANDROID_SDK_ROOT` | Racine du SDK Android | `%LOCALAPPDATA%\Android\Sdk` |
| `JAVA_HOME` | JDK 17+ (Microsoft OpenJDK recommandé) | `C:\Program Files\Microsoft\jdk-17.x.x` |
| `GAIALIFE_ANDROID_SDK` *(optionnel)* | Surcharge du chemin SDK pour les scripts Gaia-Life | même valeur que `ANDROID_HOME` |
| `GAIALIFE_AVD_NAME` *(optionnel)* | Nom d’AVD (défaut `GaiaLifeTest`) | `GaiaLifeTest` |
| `GAIALIFE_SYSTEM_IMAGE` *(optionnel)* | Image système pour `avdmanager` | `system-images;android-35;google_apis;x86_64` |

Le script résout le SDK dans cet ordre : `GAIALIFE_ANDROID_SDK` → `ANDROID_HOME` → `ANDROID_SDK_ROOT` → `%LOCALAPPDATA%\Android\Sdk` → chemins de repli.

### 3. JDK + Android SDK (si absents)

**Option A — cible MSBuild (recommandée avec le workload .NET Android)** :

```powershell
dotnet build src\App.Mobile\App.Mobile.csproj `
  -t:InstallAndroidDependencies `
  -f net9.0-android `
  -p:AcceptAndroidSDKLicenses=True `
  -p:AndroidSdkDirectory=$env:LOCALAPPDATA\Android\Sdk
```

Puis définir `ANDROID_HOME` / `JAVA_HOME` si le script ne les détecte pas automatiquement. Le script `run-emulator.ps1` passe déjà `AndroidSdkDirectory=%LOCALAPPDATA%\Android\Sdk` lors de cette étape.

**Option B — CLI MAUI expérimentale** (globale) :

```powershell
dotnet tool install -g Microsoft.Maui.Cli --prerelease
maui doctor
maui android install
```

Utile pour `maui android emulator create|start|list`. Le script Gaia-Life **préfère** cette CLI si `maui` est dans le `PATH`, sinon bascule sur `emulator` / `avdmanager` du SDK.

**Option C — Android Studio** : installer SDK + Emulator + une system image x86_64/arm64, puis pointer `ANDROID_HOME` vers le SDK.

Hyperviseur : activer **Windows Hypervisor Platform** (WHPX) ou Hyper-V pour des perfs correctes.

---

## Comportement du script

Fichiers :

- [`scripts/run-emulator.ps1`](../scripts/run-emulator.ps1) — logique complète  
- [`scripts/lancer-emulateur-gaialife.bat`](../scripts/lancer-emulateur-gaialife.bat) — lanceur double-clic

Étapes :

1. **Prérequis** : SDK / JDK / `adb`  
2. **AVD** : liste (`maui android emulator list` ou `emulator -list-avds`) → crée `GaiaLifeTest` si absent  
3. **Démarrage** : si aucun `emulator-*` dans `adb devices` → start + attente `sys.boot_completed=1`  
4. **Déploiement** :

```powershell
dotnet build src\App.Mobile\App.Mobile.csproj -t:Run -f net9.0-android `
  -p:AndroidSdkDirectory=<sdk> `
  -p:JavaSdkDirectory=<jdk>   # si JAVA_HOME connu
```

En cas d’échec, un message clair s’affiche (SDK manquant, AVD, boot timeout, build) et un log est écrit sous `scripts/.emulator-logs/`.

Options PowerShell :

```powershell
.\scripts\run-emulator.ps1 -SkipRun              # émulateur seulement
.\scripts\run-emulator.ps1 -AvdName MonAvd
.\scripts\run-emulator.ps1 -BootTimeoutSeconds 300
```

---

## Recréer l’émulateur `GaiaLifeTest` (installation propre)

Si l’AVD est corrompu ou trop ancien :

### Avec CLI `maui`

```powershell
maui android emulator stop --name GaiaLifeTest
maui android emulator delete --name GaiaLifeTest
maui android emulator create --name GaiaLifeTest
.\scripts\run-emulator.ps1
```

### Avec outils SDK

```powershell
# Arrêter les instances
adb emu kill

# Supprimer l’AVD
avdmanager delete avd -n GaiaLifeTest
# ou supprimer le dossier :
#   %USERPROFILE%\.android\avd\GaiaLifeTest.avd
#   %USERPROFILE%\.android\avd\GaiaLifeTest.ini

.\scripts\run-emulator.ps1
```

Le script recréera l’AVD au prochain lancement.

---

## Vérifier le fichier SQLite de l’app

ApplicationId : `com.gaialife.mobile`  
Fichier attendu : `gaialife.db` sous le stockage app (MAUI `FileSystem.AppDataDirectory` → en pratique `files/` du package).

Avec l’émulateur démarré et l’app lancée au moins une fois :

```powershell
# Remplacer adb par le binaire du SDK si besoin :
#   & "$env:ANDROID_HOME\platform-tools\adb.exe" ...

adb shell run-as com.gaialife.mobile ls -la files/
adb shell run-as com.gaialife.mobile ls -la files/gaialife.db

# Copier hors de l’appareil pour inspection (si run-as le permet) :
adb exec-out run-as com.gaialife.mobile cat files/gaialife.db > gaialife-from-emulator.db
```

Si `run-as` échoue (build Release / non débogable), utiliser un build Debug (défaut du script) ou :

```powershell
adb shell "run-as com.gaialife.mobile pwd"
```

Pour confirmer que le processus tourne :

```powershell
adb shell pidof com.gaialife.mobile
adb logcat -d | Select-String -Pattern 'gaialife|SQLite|Maui'
```

---

## Dépannage rapide

| Symptôme | Piste |
| --- | --- |
| `JAVA_HOME` non détecté | Le script cherche aussi `C:\Program Files\Android\openjdk\jdk-*` (JDK du tooling .NET Android). Sinon installer Microsoft OpenJDK 17+ et définir `JAVA_HOME`. |
| `adb introuvable` | Installer `platform-tools` dans le SDK |
| Timeout boot | WHPX/Hyper-V ; augmenter `-BootTimeoutSeconds` ; fermer les anciens `qemu` |
| `NETSDK` / workload | `dotnet workload install maui` puis redémarrer le terminal |
| Build OK mais app absente | Vérifier `adb devices` = `device` (pas `offline`) ; relancer le `.bat` |
| Échec création AVD | Installer une system image x86_64 ; ou `maui android sdk install emulator` |

Référence contrat mobile : [`docs/CONTRAT-MIGRATION-MOBILE.md`](CONTRAT-MIGRATION-MOBILE.md).
