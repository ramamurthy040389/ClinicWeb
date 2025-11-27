# Clinic.Web

Clinic.Web is a simple ASP.NET Core MVC application for managing doctor appointments in a clinic. Patients can book appointments online, and admins can manage the doctor roster, schedules, and patient details from the browser.

## Features

- **Public booking page**
  - Choose doctor, date/time, duration
  - Capture patient info (name, phone, address, DOB, gender, optional file number)
  - Shows available slots per doctor
  - Prevents overlapping appointments

- **Admin portal**
  - Login via credentials in `appsettings.json`
  - Manage appointments (list, filter, edit, delete)
  - Manage doctors (CRUD with search and pagination)
  - Edit patient details per appointment without affecting other records

- **API endpoints**
  - `/api/appointments` for booking/fetching/maintaining appointments
  - `/api/doctors` for doctor info (public) plus `/api/doctors/admin` for paged admin view
  - `/api/patients/{fileNo}` for quick lookup

- **UI**
  - Modern Bootstrap 5 design with responsive cards/tables/modals
  - Loading states, progress feedback, client-side validation

## Tech stack

- .NET 8 / ASP.NET Core MVC
- Entity Framework Core with SQL Server (LocalDB connection string by default)
- Bootstrap 5 + vanilla JS (no frontend framework)
- Cookie authentication / role-based authorization

## Getting started

### Prerequisites
- .NET 8 SDK
- SQL Server LocalDB (or update connection string in `appsettings.json`)

### Setup

# restore & build
dotnet restore
dotnet build

# apply EF Core migrations
dotnet ef database update

# run the app
dotnet run --urls "http://localhost:5000"The database is auto-created via EF migrations. If you already ran the app before these migrations existed and get duplicate-index errors, delete the `ClinicBookingDb` database or clean up the duplicates before rerunning `dotnet ef database update`.

### Admin login

Default credentials (stored in `appsettings.json`):

- **Username:** `admin@clinic.local`
- **Password:** `Admin@123`

> ⚠️ Change these for production!

## Project structure

restore & build
dotnet restore
dotnet build

apply EF Core migrations
dotnet ef database update
run the app
dotnet run --urls "http://localhost:5000"


The database is auto-created via EF migrations. If you already ran the app before these migrations existed and get duplicate-index errors, delete the `ClinicBookingDb` database or clean up the duplicates before rerunning `dotnet ef database update`.

### Admin login

Default credentials (stored in `appsettings.json`):

- **Username:** `admin@clinic.local`
- **Password:** `Admin@123`

> ⚠️ Change these for production!

## Project structure
ctor list |
| `/api/doctors/admin` | `GET` | Admin paged list (requires auth) |
| `/api/doctors` | `POST`/`PUT`/`DELETE` | Manage doctors (admin) |
| `/api/patients/{fileNo}` | `GET` | Lookup patient by file number |

All admin endpoints require authentication (`Admin` role). The booking endpoint is anonymous but validates phone/date/time and overlap.

## Notes / tips

- File numbers are optional; each booking creates its own patient record to avoid cross-appointment edits.
- When editing appointments in the admin UI, if you need the patient to inherit an existing file number, supply a unique one or leave it blank to keep it empty.
- If you change the connection string or authentication scheme, update `appsettings.json` accordingly.

---

Feel free to expand this README with screenshots, deployment instructions (Docker, Azure, etc.), or contributing guidelines if you plan to collaborate with others.

Controllers/
AdminController.cs
AccountController.cs
Api/
AppointmentsController.cs
DoctorsController.cs
PatientsController.cs
Data/
ClinicContext.cs
DTOs/
BookingRequest.cs // Booking + update DTOs, paging helpers
Models/
Appointment.cs
Doctor.cs
Patient.cs
Migrations/
Services/
AppointmentService.cs
Views/
Booking/Index.cshtml
Admin/Appointments.cshtml
Doctors/Index.cshtml
...
wwwroot/
css/site.css
js/app.js // booking + patient search + admin list loader
js/admin-appointments.js
js/doctors.js



## API quick reference

| Endpoint | Method | Description |
| --- | --- | --- |
| `/api/appointments` | `GET` | Paged list (supports filters) |
| `/api/appointments` | `POST` | Create appointment (public) |
| `/api/appointments/{id}` | `GET` | Detailed appointment |
| `/api/appointments/{id}` | `PUT` | Update appointment (schedule + patient) |
| `/api/appointments/{id}` | `DELETE` | Cancel appointment |
| `/api/doctors` | `GET` | Public doctor list |
| `/api/doctors/admin` | `GET` | Admin paged list (requires auth) |
| `/api/doctors` | `POST`/`PUT`/`DELETE` | Manage doctors (admin) |
| `/api/patients/{fileNo}` | `GET` | Lookup patient by file number |

All admin endpoints require authentication (`Admin` role). The booking endpoint is anonymous but validates phone/date/time and overlap.

## Notes / tips

- File numbers are optional; each booking creates its own patient record to avoid cross-appointment edits.
- When editing appointments in the admin UI, if you need the patient to inherit an existing file number, supply a unique one or leave it blank to keep it empty.
- If you change the connection string or authentication scheme, update `appsettings.json` accordingly.

---

Feel free to expand this README with screenshots, deployment instructions (Docker, Azure, etc.), or contributing guidelines if you plan to collaborate with others.

dotnet ef migrations add InitialCreate
dotnet ef database update
 
dotnet run --urls "http://localhost:5000"

dotnet clean
dotnet build


"Username": "admin@clinic.local",
"Password": "Admin@123" // change to a stronger password in real use



after login you can get the more tab's 
-> Manage Doctors 
-> Appointments
-> Patient Search 

dotnet ef migrations add InitialCreate
dotnet ef database update
 
dotnet run --urls "http://localhost:5000"

dotnet clean
dotnet build


"Username": "admin@clinic.local",
"Password": "Admin@123" // change to a stronger password in real use



after login you can get the more tab's 
-> Manage Doctors 
-> Appointments
-> Patient Search 
