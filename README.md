# Progetto Gestione Accessi 
> Obiettivo: creare un sistema (simulato) che gestisca gli accessi ad un edificio.

## Componenti previsti:
- Lettore RFID
- 1x Led RGB
- 1x Sensore PIR
- 1x Servomotore
- 1x Buzzer
- 3x Arduino ("Mega2560")
- Software Vario

## Spiegazione del Progetto
Il progetto consiste nel rappresentare la gestione degli accessi ad un edificio. Si ipotizza quindi che all'ingresso dell'edificio venga posto un **lettore RFID**, in modo da poter avere uno storico degli accessi ed evitare accessi non autotizzati.  
Ci sarà quindi un **LED (RGB)** che si illuminerà di <span style="color: lightgreen;">verde</span> ogni volta che verrà autorizzato un ingresso, mentre lampeggerà di <span style="color: red;">rosso</span> se si tenta di accedere con un 'token' non presente nel database.  

Il **servomotore** simulerà l'apertura della porta / cancello in ingresso, quindi un **sensore PIR** posto all'interno dell'edificio rileverà il passaggio della persona e procederà a bloccare la porta.  

Il sistema rileverà come *"Accesso non autorizzato"* se dovesse essere rilevato del movimento tramite il **sensore PIR** ma non autorizzato all'ingresso e/o non avendo aperto la porta. Partirà dunque il sistema di allarme, facendo lampeggiare di <span style="color: red;">rosso</span> il **LED RGB** e facendo suonare il **buzzer**
