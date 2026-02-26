<img width="1515" height="524" alt="image" src="https://raw.githubusercontent.com/siliciium/CallTrap/refs/heads/main/Windows/images/2.png" />

<br>
<br>

## ⚡︎ Il s'agit d'un softphone simple que vous pouvez utiliser pour communiquer avec Asterisk lors de vos démonstrations ou tests.

- Vous avez besoin de Visual Studio pour compiler le projet.
- Cet outil utilise [SIPSorcery](https://github.com/sipsorcery-org/sipsorcery), qui est distribué sous cette [licence](https://github.com/sipsorcery-org/sipsorcery/blob/master/LICENSE.md).

<br>
<br>

<img width="975" height="600" alt="Sans-titre-2026-02-23-0652-github" src="https://raw.githubusercontent.com/siliciium/CallTrap/refs/heads/main/Windows/images/3.png" />

<br>
<br>

## ⚠︎ Attention :
Le projet gère uniquement le système de quantification logarithmique : Pulse Code Modulation [A-Law](https://fr.wikipedia.org/wiki/Loi_A), part of the G.711 audio codec (PCMA)

<br>
<br>

## ⚡︎ Exemple d'utilisation :

* Simuler un appel avec le numéro `+33999999999` et utiliser un fichier audio local à la place du microphone
```
PS> .\SoftPhone.exe --sip-server calltrap.rpi --sip-user 1000 --sip-pwd "00000000000" --callnum +33999999999 --audio-file "$env:USERPROFILE\voice.wav"
```

<br>

* Simuler un appel avec le numéro `+33999999998` et utiliser le microphone de votre ordinateur
```
PS> .\SoftPhone.exe --sip-server calltrap.rpi --sip-user 1000 --sip-pwd "00000000000" --callnum +33999999999 --mic
```

<br>

* Simuler un appel avec le numéro `+33999999997` , utiliser le microphone et enregistrer l'appel dans un fichier .wav (mix entrant/sortant)
```
PS> .\SoftPhone.exe --sip-server calltrap.rpi --sip-user 1000 --sip-pwd "00000000000" --callnum +33999999997 --mic --rec
```
<br>
Les appels sont enregistrés dans le répertoire Téléchargements de l'utilisateur.
https://github.com/siliciium/CallTrap/blob/c32f94e58b9ba534643d4004a4335c249f611ee7/Windows/Program.cs#L728


<br>
<br>

**(extension Asterisk appropriée nécessaire pour gérer les appels)*

<br>
<br>

# Demo :
<img width="1279" height="548" alt="image" src="https://raw.githubusercontent.com/siliciium/CallTrap/refs/heads/main/Windows/images/4.png" />

