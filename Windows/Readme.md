<img width="1279" height="548" alt="image" src="https://github.com/user-attachments/assets/52c1ef26-325e-45cd-ab19-1bfb9d91013c" />

<br>
<br>

## ⚡︎ Il s'agit d'un softphone simple que vous pouvez utiliser pour communiquer avec Asterisk lors de vos démonstrations ou tests.

- Vous avez besoin de Visual Studio pour compiler le projet.
- Cet outil utilise [SIPSorcery](https://github.com/sipsorcery-org/sipsorcery), qui est distribué sous cette [licence](https://github.com/sipsorcery-org/sipsorcery/blob/master/LICENSE.md).

<br>
<br>

## ⚠︎ Attention :
Le projet gère uniquement le système de quantification logarithmique : Pulse Code Modulation [A-Law](https://fr.wikipedia.org/wiki/Loi_A), part of the G.711 audio codec (PCMA)

<br>
<br>

## ⚡︎ Exemple d'utilisation :

<img width="1515" height="524" alt="image" src="https://github.com/user-attachments/assets/7d1a47b5-f1ea-47ba-9379-232b7da1fed6" />

<br>
<br>

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
Les appels sont enregistrés dans le répertoire Téléchargements https://github.com/siliciium/CallTrap/blob/c32f94e58b9ba534643d4004a4335c249f611ee7/Windows/Program.cs#L728
de l'utilisateur.

<br>
<br>

**(extension Asterisk appropriée nécessaire pour gérer les appels)*

