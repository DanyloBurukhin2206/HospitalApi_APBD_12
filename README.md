# HospitalApi — APBD Tutorial 8

## Opis zadania

Projekt jest prostą aplikacją **ASP.NET Core Web API** przygotowaną do zadania APBD Tutorial 8.
Aplikacja korzysta z bazy danych SQL Server utworzonej na podstawie pliku `Database/create.sql`.

Zaimplementowane endpointy:

1. `GET /api/patients`  
   Zwraca listę pacjentów razem z przyjęciami do szpitala oraz przypisaniami łóżek.

2. `GET /api/patients?search=tekst`  
   Zwraca pacjentów filtrowanych po `FirstName` lub `LastName`. Filtrowanie jest wykonane przez `EF.Functions.Like`, czyli odpowiednik SQL `LIKE '%tekst%'`.

3. `POST /api/patients/{pesel}/bedassignments`  
   Przypisuje pacjentowi wolne łóżko danego typu na podanym oddziale i w podanym przedziale czasu.

---

## Technologie

- C#
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- Swagger

---

## Struktura projektu

```text
HospitalApi/
├── Controllers/
│   └── PatientsController.cs
├── Data/
│   └── HospitalDbContext.cs
├── Database/
│   └── create.sql
├── Dtos/
├── Models/
├── Services/
├── docs/
│   ├── Hospital-ERD.png
│   ├── GET.json
│   └── POST.json
├── Program.cs
├── appsettings.json
└── HospitalApi.csproj
```

---

## Jak uruchomić bazę danych

### Opcja 1 — SQL Server Management Studio / Azure Data Studio

1. Uruchom SQL Server.
2. Utwórz nową bazę danych, np. `HospitalDb`.
3. Otwórz plik:

```text
Database/create.sql
```

4. Wykonaj cały skrypt na bazie `HospitalDb`.

### Opcja 2 — terminal

Przykład dla `sqlcmd`:

```bash
sqlcmd -S localhost -E -Q "CREATE DATABASE HospitalDb"
sqlcmd -S localhost -E -d HospitalDb -i Database/create.sql
```

Jeżeli używasz logowania SQL Server zamiast Windows Authentication:

```bash
sqlcmd -S localhost -U sa -P "TwojeHaslo" -Q "CREATE DATABASE HospitalDb"
sqlcmd -S localhost -U sa -P "TwojeHaslo" -d HospitalDb -i Database/create.sql
```

---

## Konfiguracja połączenia z bazą

W pliku `appsettings.json` ustaw connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=HospitalDb;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

Dla SQL Server Express często będzie to:

```json
"Server=localhost\\SQLEXPRESS;Database=HospitalDb;Trusted_Connection=True;TrustServerCertificate=True"
```

Dla logowania przez użytkownika `sa`:

```json
"Server=localhost;Database=HospitalDb;User Id=sa;Password=TwojeHaslo;TrustServerCertificate=True"
```

---

## Database First / Scaffold

W zadaniu wymagane jest podejście Database First. Po utworzeniu bazy danych można odtworzyć modele i kontekst poleceniem:

```bash
dotnet ef dbcontext scaffold "Name=ConnectionStrings:DefaultConnection" Microsoft.EntityFrameworkCore.SqlServer --context HospitalDbContext --context-dir Data --output-dir Models --force
```

W tym projekcie modele i `HospitalDbContext` są już przygotowane, więc nie musisz wykonywać scaffoldu ponownie, jeżeli chcesz tylko uruchomić aplikację.

---

## Uruchomienie aplikacji

W folderze projektu wykonaj:

```bash
dotnet restore
dotnet run
```

Następnie otwórz Swagger:

```text
http://localhost:5078/swagger
```

albo, jeżeli aplikacja uruchomi się na innym porcie, sprawdź adres wypisany w terminalu.

---

## Przykładowe zapytania

### GET all patients

```http
GET http://localhost:5078/api/patients
```

### GET patients with search

```http
GET http://localhost:5078/api/patients?search=an
```

### POST bed assignment

```http
POST http://localhost:5078/api/patients/90010112345/bedassignments
Content-Type: application/json

{
  "from": "2026-05-20T14:00:00",
  "to": "2026-05-30T10:00:00",
  "bedType": "Standard",
  "ward": "Kardiologia"
}
```

`to` jest opcjonalne:

```http
POST http://localhost:5078/api/patients/90010112345/bedassignments
Content-Type: application/json

{
  "from": "2026-06-01T09:00:00",
  "bedType": "Intensywna terapia",
  "ward": "Chirurgia"
}
```

---

## Zasada sprawdzania wolnego łóżka

Łóżko jest wolne, jeżeli nie istnieje inne przypisanie, którego termin nachodzi na nowy termin.

Dla nowego przedziału z datą `to` sprawdzany jest warunek kolizji:

```text
existing.From < request.To && (existing.To == null || existing.To > request.From)
```

Dla nowego przedziału bez daty `to` traktujemy go jako przypisanie bezterminowe.

---

## Kody odpowiedzi

- `200 OK` — lista pacjentów została zwrócona poprawnie.
- `201 Created` — łóżko zostało przypisane pacjentowi.
- `400 Bad Request` — błędne dane, np. `to` wcześniejsze niż `from`.
- `404 Not Found` — pacjent, oddział, typ łóżka albo wolne łóżko nie zostało znalezione.

