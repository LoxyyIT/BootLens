# Security policy

## Modifiche a startup

BootLens richiede elevation all’avvio perché alcune fonti Windows richiedono privilegi amministrativi. Le operazioni supportate sono limitate a voci Registry Run/RunOnce eleggibili, Startup folder, Scheduled Tasks e servizi; creano backup dove previsto, verificano il risultato e registrano un undo record quando disponibile.

Servizi, driver, Winlogon, Shell, WMI e componenti di sicurezza sono rilevati o riservati a modalità avanzata, ma non sono modificabili dalle azioni semplici.

## Dati e rete

Il core offline non dipende dalla rete. Community, telemetria, upload crash e VirusTotal sono OFF di default. Il client Community non invia nulla quando non è abilitato e non accetta percorsi personali completi o contenuti file.

## Segnalazioni

Non pubblicare dettagli sfruttabili in issue pubbliche. Apri una segnalazione privata al maintainer del progetto con versione, riproduzione minima, log non sensibili e impatto. Non allegare database, token o percorsi personali non necessari.

## Stato della revisione

La scansione statica locale e i test coprono il percorso delle modifiche Registry utente, ma non sostituiscono una revisione indipendente, code signing, test su più versioni Windows o una verifica E2E del servizio Community in produzione.
