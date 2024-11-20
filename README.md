Progetto Gestione Accessi - Nuova Specifica

    Obiettivo: creare una piattaforma per il monitoraggio e la gestione degli accessi a un edificio.

Componenti previsti

    Lettore RFID per identificare gli utenti
    1x Led RGB per indicazioni visive dello stato di accesso
    1x Sensore PIR per il rilevamento della presenza (opzionale)
    1x Servomotore per simulare l'apertura della porta/cancello
    1x Buzzer per segnalazioni acustiche
    3x Arduino Mega2560 per la gestione delle funzioni hardware
    Software per il backend e interfaccia web (sviluppata in .NET)

Funzionalità del Sistema

    Monitoraggio degli Accessi:
        Ogni accesso viene registrato in un log digitale che mostra:
            Utente: nome associato al token RFID (se disponibile).
            Codice RFID: identificativo univoco del token.
            Data e ora: momento esatto dell'accesso.

    Gestione degli Accessi Manuali:
        Pulsante nell'interfaccia web per aprire manualmente la porta/cancello senza l'uso di un token RFID.

    Indicazioni Visive:
        LED RGB:
            Verde: accesso autorizzato.
            Rosso: accesso negato.

    Simulazione Apertura Porta:
        Servomotore: attivato solo per gli accessi autorizzati.

    Registro degli Accessi:
        Piattaforma per visualizzare e filtrare lo storico degli accessi con opzioni di ricerca avanzata.