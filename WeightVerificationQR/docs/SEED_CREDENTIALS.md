# Default Seed Credentials

On first run, `DbInitializer` creates the database and seeds one Admin account:

| Field    | Value       |
|----------|-------------|
| Username | `admin`     |
| Password | `Admin@123` |

The password is hashed with PBKDF2 (SHA-256, 100,000 iterations, random 16-byte
salt per user — see `PasswordHasher.cs`); it is never stored in plain text.

## Do this before going live on the production floor

1. Log in as `admin`.
2. Go to **User Management** and either change the admin password or create a
   named Admin account for yourself and deactivate the generic `admin` login.
3. Create Operator/Supervisor accounts for each shift worker with their own
   credentials — the spec calls for per-operator audit trails (`OperatorName`
   on every `WeighRecord`), which only works if each person logs in as
   themselves rather than sharing `admin`.
4. Set a real COM port and printer IP/COM port under **Machine Settings** /
   **Printer Settings** (both Admin-only).
