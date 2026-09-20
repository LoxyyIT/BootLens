# Community self-hosting — Coming soon

Le funzioni Community sono temporaneamente disattivate e non fanno parte del prodotto attuale. Il server e il client restano documentati come fondazione futura, ma non devono essere avviati o pubblicati finché la funzione non sarà riattivata dopo la revisione di privacy, retention, autenticazione, TLS e abuso.

Quando la funzione arriverà, l’app dovrà restare Community OFF di default e mostrare prima la preview del payload: `item_hash`, `mechanism`, `action` e una categoria publisher opzionale.

La procedura locale è: copiare `server/.env.example` in `server/.env`, eseguire `docker compose --env-file server/.env -f server/docker-compose.yml up --build -d` e verificare `http://127.0.0.1:8080/health`.

Il database è il volume `bootlens-community-data`. Eseguire backup fermando il container o usando una copia consistente di `community.db`; conservare anche il file `.env` fuori dal controllo versione.

L’API non riceve username, nome PC, documenti, seriali, product key, contenuti file o percorsi completi. Per un deployment pubblico servono reverse proxy TLS, autenticazione/rate limiting, policy di retention, backup verificati e monitoraggio: non sono attivati automaticamente da questo scaffold locale.
