<p align="center">
<img alt="image" width="700" src="https://github.com/siliciium/CallTrap/blob/main/Windows/PBAP-PCE/Images/_2.png" />
</p>

# ⚡︎ Ce programme permet de récupérer la liste des appels (ICH/MCH) via Bluetooth et le profile PBA (Phone Book Access) et d'analyser les appels et exporter une liste d'appels. 
- **ICH** : Incoming Call History
- **MCH** : Missing Call History

## ⚡︎ Requis
- Vous avez besoin de Visual Studio pour compiler le projet.
- Vous avez besoin de [Windows SDK for Windows 10 2004 (10.0.19041.0)](https://go.microsoft.com/fwlink/?linkid=2311805) pour `Windows.winmd`
> Type
- `<TargetFrameworkVersion>v4.8.1</TargetFrameworkVersion>`
> Références
- `..\..\..\..\..\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.WindowsRuntime.dll`
- `..\..\..\..\..\Program Files (x86)\Windows Kits\10\UnionMetadata\10.0.19041.0\Windows.winmd`

# ⚡︎ Utilisation
- L'appareil Bluetooth pour lequel vous souhaitez obtenir les informations (probablement un smartphone) doit être appairé avant de pouvoir procéder à une analyse. Lors de l'appairage vous devez vous assurez de cocher `l'autorisation Bluetooth liée au partage du carnet d’adresses` sur l'appareil distant. Le nom exact varie selon les marques, mais les systèmes Android utilisent des formulations proches.

- **Les libellés les plus courants :**
  - Accès au carnet d’adresses
  - Partager les contacts
  - Synchronisation des contacts
  - Autoriser l’accès aux contacts
  - Accès au répertoire téléphonique
  - Téléchargement du carnet d’adresses
  - Phonebook access (sur certains modèles)


- **Où trouver l’option PBAP ?**

  Le chemin est presque toujours le même :

      1. Paramètres
      2. Bluetooth
      3. Appuyer sur l’icône ⚙️ ou Options du périphérique déjà appairé
      4. Activer l’option liée au partage des contacts / carnet d’adresses

- **Champs**
  - `Client Bluetooth Name` : Le nom Bluetooth de l'appareil à analyser.
  - `Location`  : Telecom, SIM1 seulement implémentés.
  - `Phonebook` : ich et/ou mch seulement implémentés.
  - `Months` : Filtre des appels depuis le nombre de mois spécifiés.
- **Boutons**
  - de gauche à droite: `Exporter`, `Plage de temps / Appels` (switch) , `Analyser`


- **Filtrer les appels par attributaire** pour observer le delta entre les appels (en sélectionnant un appel) et voir le nombre d'appels total et le nombre maximum d'appels par jour provenant du même attributaire.
<p align="center">
<img alt="image" width="700" src="https://github.com/siliciium/CallTrap/blob/main/Windows/PBAP-PCE/Images/_3.png" />
</p>

- **Filtrer les appels et obtenir le nombre d'appels** qui ont eu lieu dans une fenêtre de temps
<p align="center">
<img alt="image" width="700" src="https://github.com/siliciium/CallTrap/blob/main/Windows/PBAP-PCE/Images/_4.png" />
</p>
