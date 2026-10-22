# AbfuhrPilot

Dispositions- und Fahrer-Plattform für Entsorgungsbetriebe. Disponenten planen Touren je
Abfallfraktion (Hausmüll, Kunststoff, Holz, Papier, Bio, Sperrmüll, Container, Sonderabfall),
Fahrer arbeiten sie auf einer einfachen, tabletfähigen Oberfläche ab.

Entwickelt von **Amin Ahmed Mohamed** als eigenes Projekt (Konzept, Backend, Frontend, Tests).

## Technik

- **Backend:** .NET 8, ASP.NET Core Web API, Clean Architecture (Domain / Application /
  Infrastructure / Api), EF Core, PostgreSQL, JWT mit Refresh-Tokens, rollenbasierte Rechte
  (Admin, Disponent, Fahrer, Betrachter), Swagger
- **Frontend:** Angular 19, Standalone Components, Signals, eigenes Design-System ohne UI-Bibliothek
- **Robustheit:** Idempotency-Keys für Fahrer-Aktionen (sichere Wiederholung bei schlechtem
  Netz), Rate-Limiting für Login und Konto-Endpunkte, Mandantentrennung zwischen Firmen
- **Routing:** GraphHopper als optionaler Anbieter; ohne API-Key läuft ein lokaler Fallback
- **CI:** GitHub Actions baut und testet Backend und Frontend bei jedem Push

## Funktionen

- Leitstand mit Tagesübersicht, Problemstellen und Verteilung nach Abfallfraktion
- Routenplanung mit Stopps, Reihenfolge per Drag & Drop, Fahrzeug- und Fahrerzuordnung
- Fahrer-Ansicht: nächster Stopp, Start / Erledigt / Überspringen, Problem melden mit Foto
- Stammdaten: Container, Fahrzeuge (mit Freigabe je Abfallart), Fahrer, Abfallarten, Ziele
- Benutzerverwaltung mit Passwort-Reset und E-Mail-Bestätigung (Einmal-Tokens). Der
  Mailversand ist in dieser Version ein Platzhalter, der nur protokolliert; für echten
  Versand wird ein SMTP-Dienst hinter `IEmailSender` angebunden

## Starten

Alles in Docker:

```bash
docker compose up -d --build
# Web-App  http://localhost:4200
# API      http://localhost:5080   (Swagger unter /swagger)
```

Oder lokal ohne Datenbank (In-Memory, Demodaten werden angelegt):

```bash
cd backend
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/AbfuhrPilot.Api

cd frontend
npm install
npx ng serve
```

Demo-Zugänge (nur lokale Demodaten), Passwort jeweils `Passw0rd!`:
`admin@demo.de`, `dispo@demo.de`, `fahrer@demo.de`, `viewer@demo.de`

## Tests

```bash
cd backend && dotnet test                                    # 164 Tests
cd frontend && npx ng test --watch=false --browsers=ChromeHeadless   # 137 Tests
```

Die Backend-Tests nutzen den EF-In-Memory-Provider, es wird keine Datenbank benötigt.

## Konfiguration

`.env.example` nach `.env` kopieren und anpassen. In Produktion einen starken
`Jwt__SecretKey` setzen; die Werte in `appsettings.Development.json` und
`docker-compose.yml` sind nur für die lokale Entwicklung gedacht.

## Aufbau

```
backend/   .NET-8-Solution (Domain, Application, Infrastructure, Api, Tests)
frontend/  Angular-19-App (core, design-system, features)
```
