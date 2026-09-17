# Déploiement téléphone Android — Gaia-Life (App.Mobile)

Guide pour installer et tester l’app MAUI Blazor Hybrid (`net9.0-android`) sur un **téléphone physique**, distinct de l’émulateur [`GaiaLifeTest`](EMULATEUR-ANDROID.md).

## Lancement quotidien

1. Double-cliquer sur [`scripts/lancer-telephone-gaialife.bat`](../scripts/lancer-telephone-gaialife.bat).
2. Au démarrage, le `.bat` propose un **menu** :
   1. **USB**
   2. **Wi‑Fi** : IP + Port + Code (un seul port ; code vide = `adb connect` seul)
   3. **USB puis Wi‑Fi** : `adb tcpip 5555` puis `adb connect IP:5555`

Avec un code : `adb pair IP:PORT CODE` puis `adb connect IP:PORT` (même port). Erreur **10061** : mode **1**.
3. Ensuite le script PowerShell :
   - vérifie qu’un appareil **physique** est en statut `device` (ignore les `emulator-*`) ;
   - refuse de démarrer si `unauthorized` / `offline` / aucun téléphone ;
   - si plusieurs téléphones : demande `-DeviceSerial` (ou `GAIALIFE_DEVICE_SERIAL`) ;
   - build + déploie avec ciblage explicite : `-p:AdbTarget="-s <serial>"` ;
   - affiche ensuite un `adb logcat` filtré sur `com.gaialife.mobile` (Ctrl+C pour arrêter).

Ce flux **ne démarre pas** et **ne modifie pas** l’émulateur. Inversement, [`run-emulator.ps1`](../scripts/run-emulator.ps1) ne cible pas le téléphone.

Journaux locaux (non versionnés) : `scripts/.device-logs/`.

### Options utiles

| Option / variable | Effet |
| --- | --- |
| `-DeviceSerial SERIAL` ou `GAIALIFE_DEVICE_SERIAL` | Force le téléphone (obligatoire si plusieurs) |
| `-WifiIp` + `-WifiConnectPort` | `adb connect` seul (PC déjà associé) |
| `+ -WifiPairCode` + `-WifiPairPort` | Pairing puis connect (première fois) |
| `-SkipRun` | Vérifie seulement la détection, sans build |
| `-NoLogcat` | Déploie puis quitte sans suivre les logs |
| `-LogcatSeconds 60` | Suit logcat 60 s puis quitte (sinon jusqu’à Ctrl+C) |

```powershell
.\scripts\run-device.ps1 -DeviceSerial ABC123DEF -NoLogcat
# Deja associe :
.\scripts\run-device.ps1 -WifiIp 172.16.101.247 -WifiConnectPort 41234
# Premiere association :
.\scripts\run-device.ps1 -WifiIp 172.16.101.247 -WifiConnectPort 41234 -WifiPairPort 39169 -WifiPairCode 085993
adb devices -l
```

---

## Connexion USB — mode développeur

### 1. Activer les options pour développeurs

1. Ouvrir **Paramètres** → **À propos du téléphone** (parfois *À propos de l’appareil* / *Informations du téléphone*).
2. Trouver **Numéro de build** (ou *Version MIUI* / *Build number* selon le constructeur).
3. Appuyer **7 fois** dessus jusqu’au message du type « Vous êtes maintenant un développeur ».

### 2. Activer le débogage USB

1. Revenir aux **Paramètres** → **Système** (ou *Paramètres supplémentaires*) → **Options pour les développeurs**.
2. Activer **Options pour les développeurs** si besoin.
3. Activer **Débogage USB**.
4. Brancher le téléphone au PC avec un câble **données** (pas charge seule).
5. Sur l’écran du téléphone, accepter la fenêtre **Autoriser le débogage USB** (cocher « Toujours autoriser depuis cet ordinateur » si vous faites confiance à ce PC).

Sans cette acceptation, `adb devices` affichera `unauthorized` et le script s’arrêtera avec un message clair.

### 3. Vérifier la détection

```powershell
adb devices
```

Attendu (exemple) :

```text
List of devices attached
R58M12ABCDE    device
```

| Statut | Signification | Action |
| --- | --- | --- |
| `device` | OK | Vous pouvez lancer `run-device.ps1` |
| `unauthorized` | Dialogue non accepté | Accepter sur le téléphone, éventuellement `adb kill-server` puis rebrancher |
| `offline` | Lien HS | Autre câble/port, redémarrer adb, vérifier le mode transfert fichiers |
| *(absent)* | Pas vu par adb | Pilotes OEM / USB debugging / câble |

Si un émulateur tourne en parallèle :

```text
emulator-5554    device
R58M12ABCDE      device
```

Le script **ignore** `emulator-*` et déploie uniquement sur le téléphone (ou échoue s’il y a plusieurs téléphones sans `-DeviceSerial`).

---

## Connexion sans fil (Android 11+)

PC et téléphone sur le **même réseau Wi‑Fi**.

### Pairing (première fois)

Selon la version / le constructeur :

**A — Pairing Wi‑Fi dans Options développeurs (recommandé)**

1. **Options pour les développeurs** → **Débogage sans fil** (ou *Wireless debugging*) → activer.
2. **Associer un appareil avec un code d’association**.
3. Sur le PC :

```powershell
adb pair <IP_TELEPHONE>:<PORT_PAIRING>
```

Saisir le code à 6 chiffres affiché sur le téléphone.

4. Puis connecter (le port de connexion peut différer du port de pairing) :

```powershell
adb connect <IP_TELEPHONE>:<PORT_CONNEXION>
adb devices
```

**B — Première association avec câble puis bascule Wi‑Fi**

```powershell
adb devices
adb tcpip 5555
adb connect <IP_TELEPHONE>:5555
# débrancher le câble
adb devices
```

La première association nécessite en général un **câble** ou le **pairing / QR** affiché dans les options développeurs — pas de déploiement « magique » sans cette étape.

Ensuite : `.\scripts\run-device.ps1` comme en USB (le serial Wi‑Fi ressemble souvent à `192.168.x.x:5555`).

---

## Sécurité

Le **débogage USB / sans fil** donne un accès élevé (install d’apps, lecture de logs, parfois `run-as` selon le build).

- Utiliser un PC de confiance.
- Après les sessions de test sur un téléphone **personnel du quotidien** : désactiver **Débogage USB** et **Débogage sans fil** dans les options développeurs.
- Ne pas laisser un `adb tcpip` ouvert sur un réseau non fiable.

---

## Prérequis (mêmes bases que l’émulateur)

- .NET 9 SDK + workload `maui` / `maui-android`
- Android SDK (`ANDROID_HOME` / `GAIALIFE_ANDROID_SDK`) + `platform-tools` (`adb`)
- JDK 17+ si le build le demande (`JAVA_HOME`)

Détails d’installation SDK : voir [EMULATEUR-ANDROID.md](EMULATEUR-ANDROID.md) (section Prérequis). Le script téléphone peut aussi tenter `InstallAndroidDependencies` si le SDK est absent.

ApplicationId : `com.gaialife.mobile`

---

## Alternative : APK autonome (sans adb au quotidien)

Utile pour partager l’app à quelqu’un du foyer **sans** configurer son PC.

### 1. Build Debug (signature debug automatique)

Suffisant pour tester sur un téléphone où vous autorisez l’installation hors Play Store :

```powershell
dotnet build src\App.Mobile\App.Mobile.csproj -f net9.0-android -c Debug
```

APK typique :

```text
src\App.Mobile\bin\Debug\net9.0-android\com.gaialife.mobile-Signed.apk
```

(ou `com.gaialife.mobile.apk` selon la version du SDK — prendre le `*-Signed.apk` s’il est présent.)

### 2. Build Release signé (keystore **local**, non versionné)

1. Créer un keystore **une fois** (à garder hors git) :

```powershell
New-Item -ItemType Directory -Force -Path secrets\android | Out-Null
keytool -genkeypair -v `
  -keystore secrets\android\gaialife-upload.jks `
  -alias gaialife `
  -keyalg RSA -keysize 2048 -validity 10000 `
  -storepass "CHOISIR_UN_MOT_DE_PASSE" `
  -keypass "CHOISIR_UN_MOT_DE_PASSE" `
  -dname "CN=Gaia-Life Local,O=Gaia-Life,C=FR"
```

2. Publier :

```powershell
dotnet publish src\App.Mobile\App.Mobile.csproj `
  -f net9.0-android `
  -c Release `
  -p:AndroidKeyStore=true `
  -p:AndroidSigningKeyStore="$PWD\secrets\android\gaialife-upload.jks" `
  -p:AndroidSigningKeyAlias=gaialife `
  -p:AndroidSigningKeyPass=CHOISIR_UN_MOT_DE_PASSE `
  -p:AndroidSigningStorePass=CHOISIR_UN_MOT_DE_PASSE
```

APK / AAB typiques sous :

```text
src\App.Mobile\bin\Release\net9.0-android\publish\
```

**Ne jamais committer** le `.jks`, les mots de passe, ni un `*.keystore` réel. Le dépôt ignore déjà `secrets/android/` (et les journaux device).

### 3. Installation manuelle sur le téléphone

1. Copier l’`.apk` (USB file transfer, Drive, etc.).
2. Ouvrir le fichier sur le téléphone → autoriser **Sources inconnues** / *Installer des apps inconnues* pour l’appli source (Fichiers, Chrome…).
3. Installer Gaia-Life.
4. **Désactiver** ensuite l’autorisation « sources inconnues » pour cette source si le téléphone est un appareil du quotidien.

---

## Dépannage rapide

| Symptôme | Piste |
| --- | --- |
| `unauthorized` | Dialogue USB sur le téléphone ; révoquer les autorisations USB puis rebrancher |
| Déploiement sur l’émulateur | Utiliser `run-device.ps1` (force `-s` physique) ; ou arrêter l’émulateur |
| Plusieurs téléphones | `-DeviceSerial` / `GAIALIFE_DEVICE_SERIAL` |
| Build OK, app absente | Vérifier `adb -s SERIAL shell pm path com.gaialife.mobile` |
| Wi‑Fi perdu | Refaire `adb connect IP:PORT` ; le DHCP peut changer l’IP |

Inspecter SQLite sur appareil debug :

```powershell
adb -s <SERIAL> shell run-as com.gaialife.mobile ls -la files/
```
