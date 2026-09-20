# Build locale

Usare PowerShell dalla root. La sequenza verificata è restore, build Release, test e validazione delle quattro lingue. La pubblicazione dell’app è `win-x64`, self-contained e locale; non richiede un runtime .NET installato sul PC destinatario.

Il backend Community è una fondazione futura e al momento è disattivato: non avviarlo né pubblicarlo. Il codice può essere compilato con `dotnet publish server/BootLens.Community.Api/BootLens.Community.Api.csproj -c Release -o artifacts/community` per verifiche tecniche, ma non rappresenta una funzione disponibile del prodotto. Il compose pubblica la porta solo su `127.0.0.1` per evitare l’esposizione inbound implicita.

Nota di audit: `dotnet list package --vulnerable --include-transitive` segnala `SQLitePCLRaw.lib.e_sqlite3 2.1.11` come advisory High transitivo di `Microsoft.Data.Sqlite 10.0.0`. La versione stabile disponibile nel contesto non offre una sostituzione risolutiva; il progetto mantiene SQLite funzionale ma richiede una decisione di upgrade/provider prima della pubblicazione pubblica.
