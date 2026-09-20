# Architecture

BootLens separa il dominio dalle integrazioni Windows e dalla UI.

`BootLens.Core` contiene modelli immutabili, scoring, Wilson interval Community, change detection, privacy normalization, localizzazione e report exporter. Non conosce Registry, WPF o rete. Il Community Score aggregato non è ancora esposto nell’app: è una funzionalità pianificata, indicata come `Coming soon…`.

`BootLens.Windows` contiene scanner e operazioni Windows. Lo scanner usa Registry e filesystem per Run/RunOnce, Startup folder e superfici avanzate, ServiceController più Registry per servizi e driver-type services e `schtasks.exe` per le attività. Il modifier espone operazioni limitate e reversibili per Registry Run/RunOnce, Startup folder, Scheduled Tasks e servizi; le superfici avanzate restano read-only. Non esegue le command line rilevate.

`BootLens.Data` applica la migration SQLite iniziale e conserva entries, snapshots, changes, undo records, settings e boot measurements. La UI non accede direttamente alle query.

`BootLens.App` compone dependency injection, ViewModel MVVM e XAML. La prima schermata è informativa finché l’utente non accetta il disclaimer non preselezionato.

`BootLens.Agent` è un processo opzionale senza UI. La fondazione registra una scansione; la raccolta ETW completa è una milestone successiva.

Il codice Community è isolato dal prodotto locale, con SQLite e API `/api/v1` conservati come fondazione futura. Le funzioni sono temporaneamente disattivate: l’app non invia osservazioni, non richiede il backend e non espone Community come funzione attiva. Community Score resta `Coming soon…`.
