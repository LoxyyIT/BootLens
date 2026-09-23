# Architecture

BootLens separa il dominio dalle integrazioni Windows e dalla UI.

`BootLens.Core` contiene modelli immutabili, scoring, Wilson interval Community, change detection, privacy normalization, localizzazione e report exporter. Non conosce Registry, WPF o rete. Il Community Score aggregato non è ancora esposto nell’app: è una funzionalità pianificata, indicata come `Coming soon…`.

`BootLens.Windows` contiene scanner e operazioni Windows. Lo scanner usa Registry e filesystem per Run/RunOnce, Startup folder e superfici avanzate, ServiceController più Registry per servizi e driver-type services, `schtasks.exe` per le attività e CIM/WMI per `Win32_StartupCommand`. WMI records già rappresentati da un percorso/comando equivalente sono soppressi dall’elenco e conteggiati nella diagnostica della fonte. La misura dell’avvio legge `BootTime` e `BootStartTime` dall’evento 100 di `Microsoft-Windows-Diagnostics-Performance/Operational`; se il log non è leggibile o non contiene un dato valido, non viene creata una misura. Il modifier espone operazioni limitate e reversibili per Registry Run/RunOnce, Startup folder, Scheduled Tasks e servizi; le superfici avanzate restano read-only. Non esegue le command line rilevate.

`BootLens.Data` applica migration SQLite compatibili con i database esistenti e conserva entries, stato Authenticode, snapshots, changes, undo records, settings e boot measurements. La UI non accede direttamente alle query.

`BootLens.App` compone dependency injection, ViewModel MVVM e XAML. La prima schermata è informativa finché l’utente non accetta il disclaimer non preselezionato.

`BootLens.Agent` è un processo opzionale senza UI che registra una scansione. La UI legge invece le misure di boot già registrate da Windows; l’attribuzione ETW alle singole applicazioni resta una milestone successiva.

Il monitoraggio modifiche usa uno scan ogni 15 minuti mentre la finestra resta aperta, solo se l’utente lo abilita. Non installa un servizio o un’attività pianificata. L’import Autoruns è locale: compara righe CSV per categoria e percorso eseguibile normalizzato, quindi non dimostra equivalenza tra i due motori di scansione.

Il codice Community è isolato dal prodotto locale, con SQLite e API `/api/v1` conservati come fondazione futura. Le funzioni sono temporaneamente disattivate: l’app non invia osservazioni, non richiede il backend e non espone Community come funzione attiva. Community Score resta `Coming soon…`.
