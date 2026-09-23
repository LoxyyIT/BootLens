# Roadmap

## Implementato nella fondazione locale

- Scansione offline e persistenza SQLite.
- Primo avvio con consenso, ricerca, dettaglio e azioni prudenti.
- Localizzazione EN/IT/ES/FR e test automatico delle chiavi.
- Scoring prudente, segnali di affidabilità, duplicati e broken entry.
- Change detection, snapshot storage, boot measurement storage ed export.
- Durata avvio reale da Diagnostics-Performance Event 100, fingerprint della configurazione e confronto delle misure solo a parità di configurazione.
- Stato firma Authenticode distinto tra valida, assente, non valida e non verificabile; filtri per file mancanti e percorsi duplicati.
- Monitoraggio opzionale ogni 15 minuti durante l’esecuzione dell’app.
- Scansione WMI Win32_StartupCommand e pagina Copertura con conteggi e categorie non implementate.
- Import CSV Autoruns con confronto locale per categoria e percorso normalizzato.
- Struttura Community isolata, ma funzioni temporaneamente disattivate e non disponibili nell’app.

## Prossimi incrementi

- Restore UI completo per Undo Center e Snapshot.
- Misure ETW più dettagliate per attribuire CPU, I/O e memoria ai singoli processi di avvio.
- Copertura fixture più ampia e verifiche reali su più edizioni/versioni di Windows.
- Monitoraggio delle modifiche anche quando la UI non è aperta, tramite componente di background configurabile.
- Delay startup gestito e reversibile per applicazioni compatibili.
- Tema System/Light/Dark e reduced-motion configurabili dalla UI.
- Firma del codice, installer verificato e test su più SKU/versioni Windows.
- Riattivazione delle funzioni Community, backend, privacy review e Community Score aggregato (Coming soon…).
