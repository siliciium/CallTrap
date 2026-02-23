<img width="1515" height="524" alt="image" src="https://github.com/user-attachments/assets/7d1a47b5-f1ea-47ba-9379-232b7da1fed6" />

<br>
<br>


## Il s'agit d'un softphone simple 📞 que vous pouvez utiliser pour communiquer avec Asterisk lors de vos démonstrations ou tests.

- Vous avez besoin de Visual Studio pour compiler le projet.
- Cet outil utilise [SIPSorcery](https://github.com/sipsorcery-org/sipsorcery), qui est distribué sous cette [licence](https://github.com/sipsorcery-org/sipsorcery/blob/master/LICENSE.md).

<br>
<br>


## Exemple d'utilisation :

* Simuler un appel avec le numéro `+33999999999` et utiliser un fichier audio local à la place du microphone
```
PS> .\SoftPhone.exe --sip-server calltrap.rpi --sip-user 1000 --sip-pwd "00000000000" --callnum +33999999999 --audio-file "$env:USERPROFILE\voice.wav"
```
* Simuler un appel avec le numéro `+33999999998` et utiliser le microphone de votre ordinateur
```
PS> .\SoftPhone.exe --sip-server calltrap.rpi --sip-user 1000 --sip-pwd "00000000000" --callnum +33999999999 --mic
```
* Simuler un appel avec le numéro `+33999999997` et utiliser le microphone et enregistrer l'appel dans un fichier .wav (mix entrant/sortant)
```
PS> .\SoftPhone.exe --sip-server calltrap.rpi --sip-user 1000 --sip-pwd "00000000000" --callnum +33999999997 --mic --rec
```
*(extension Asterisk appropriée nécessaire pour gérer les appels)*

