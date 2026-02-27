<p align="center">
<img width="1515" height="524" alt="image" src="https://github.com/siliciium/CallTrap/blob/main/Windows/PhoneSim/Images/_1.png" />
</p>

## ⚡︎ Ce programme simule les profiles Bluetooth HFP (Hands Free Profile) et PBAP (PhoneBook Access Profile) d'un smartphone. 
Il peut être utilisé avec le module [chan_mobile](https://docs.asterisk.org/Configuration/Channel-Drivers/Mobile-Channel/Mobile-Channel-Concepts/) Asterisk pour tester une extension et/ou avec python-dbus pour l'accès à l'annuaire téléphonique. Voir ici pour [PBAP-PCE](https://github.com/siliciium/Python_Public/blob/main/Bluetooth/PBAP_PCE.py)

**Ne prend pas en charge de canal SCO (Synchronous Connection-Oriented) utilisé pour transporter de l’audio voix (CVSD, mSBC, etc.)*


## ⚡︎ Requis
- Vous avez besoin de Visual Studio pour compiler le projet.
- Vous avez besoin de [Windows SDK for Windows 10 2004 (10.0.19041.0)](https://go.microsoft.com/fwlink/?linkid=2311805) pour `Windows.winmd`
> Type
- `<TargetFrameworkVersion>v4.8.1</TargetFrameworkVersion>`
> Références
- `..\..\..\..\..\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.WindowsRuntime.dll`
- `..\..\..\..\..\Program Files (x86)\Windows Kits\10\UnionMetadata\10.0.19041.0\Windows.winmd`


## ⚡︎ Scénario
<p align="center">
<img alt="image" src="https://github.com/siliciium/CallTrap/blob/main/Windows/PhoneSim/Images/_3.png" />
</p>

## ⚡︎ Exemple
<p align="center">
<img alt="image" src="https://github.com/siliciium/CallTrap/blob/main/Windows/PhoneSim/Images/_2.png" />
</p>


## ⚡︎ Asterisk `chan_mobile.conf` exemple :
```
[general]
interval=30

[adapter]
;USB (hci1, raspberrypi)
id=raspberrypi
address=XX:XX:XX:XX:XX:XX

[phone]
address=11:11:22:22:33:11
port=4
context=from-phonesim
adapter=raspberrypi
```
- Explications `[adapter]`:
```
address=XX:XX:XX:XX:XX:XX  🡐 RaspberryPi Bluetooth adapter address
```
- Explications `[phone]`:
```
address=11:11:22:22:33:11  🡐 Windows Bluetooth adapter address
port=4                     🡐 Windows PhoneSim HFP port
```
- Vous pouvez trouver les informations dans la barre de titre de PhoneSim :
<p align="center">
<img alt="image" src="https://github.com/siliciium/CallTrap/blob/main/Windows/PhoneSim/Images/_4.png" />
</p>

- Ou depuis Linux / RaspberryPi

`user@raspberrypi:~$ sdptool browse 11:11:22:22:33:11`
```
Service Name: Handsfree Audio Gateway
Service Description: Simulated Hands-Free Phone
Service Provider: PhoneSim
Service RecHandle: 0x10181
Service Class ID List:
  UUID 128: 04a36a5f-84d5-4ef0-9272-1586994685b1
Protocol Descriptor List:
  "L2CAP" (0x0100)
  "RFCOMM" (0x0003)
    Channel: 4             🡐 Windows PhoneSim HFP port
Profile Descriptor List:
  "Handsfree" (0x111e)
    Version: 0x0107
```

## ⚡︎ Asterisk `extensions.conf` exemple :
```
[from-phonesim]
; Call without number
exten => s,1,NoOp(Call without DID callerid:${CALLERID(num)} exten:${EXTEN})
same => n,GotoIf($[ "${CALLERID(num)}" =~ "^\+33" ]?international_fr,1)
same => n,GotoIf($[ "${CALLERID(num)}" =~ "^0[976]" ]?national_fr,1)
same => n,Goto(invalid,1)

exten => international_fr,1,NoOp(International call FR)
same => n,Answer()
same => n,Playback(hello-world)
same => n,Hangup()

exten => national_fr,1,NoOp(National call FR)
same => n,Answer()
same => n,Playback(hello-world)
same => n,Hangup()

exten => invalid,1,NoOp(Non-managed call)
same => n,Hangup()
```
