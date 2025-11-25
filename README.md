
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
