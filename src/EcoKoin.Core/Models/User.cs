using System.Security.Cryptography;

namespace EcoKoin.Models
{
    /// <summary>
    /// App user. Spec: docs/03-domain-model.md (User).
    /// Password hash format: PBKDF2 "iter.salt.hash" (base64 salt/hash). Plain password is never stored.
    /// </summary>
    public class User
    {
        public const int XpPerLevel = 500;

        private const int HashIterations = 100_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public User(string userId, string username, string passwordHash, int totalEcoKoin, DateTime joinDate)
        {
            UserId = userId;
            Username = username;
            PasswordHash = passwordHash;
            TotalEcoKoin = totalEcoKoin;
            JoinDate = joinDate;
        }

        /// <summary>Creates a new user: new Guid id, PBKDF2 hash of <paramref name="password"/>, JoinDate = clock.Now, 0 koin.</summary>
        public static User Register(string username, string password, Services.IClock? clock = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username is required.", nameof(username));
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password is required.", nameof(password));

            clock ??= new Services.SystemClock();
            return new User(Guid.NewGuid().ToString(), username.Trim(), HashPassword(password), 0, clock.Now);
        }

        public string UserId { get; }
        public string Username { get; }
        public string PasswordHash { get; }
        public DateTime JoinDate { get; }

        /// <summary>Balance. Only changes through <see cref="AddEcoKoin"/>.</summary>
        public int TotalEcoKoin { get; private set; }

        /// <summary>Today's quests (1 -- *).</summary>
        public List<DailyQuest> Quests { get; } = new();

        /// <summary>User 1 -- 1 EcoPet.</summary>
        public EcoPet? Pet { get; set; }

        /// <summary>Level = TotalEcoKoin / 500 + 1.</summary>
        public int Level => TotalEcoKoin / XpPerLevel + 1;

        /// <summary>XpInLevel = TotalEcoKoin % 500.</summary>
        public int XpInLevel => TotalEcoKoin % XpPerLevel;

        /// <summary>Adds to the balance. amount &lt;= 0 throws ArgumentOutOfRangeException.</summary>
        public void AddEcoKoin(int amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
            TotalEcoKoin = checked(TotalEcoKoin + amount);
        }

        public int GetBalance()
        {
            return TotalEcoKoin;
        }

        /// <summary>True when u equals Username and PBKDF2(p) matches PasswordHash.</summary>
        public bool Login(string u, string p)
        {
            if (u is null || p is null || !string.Equals(u, Username, StringComparison.Ordinal))
                return false;
            return VerifyPassword(p, PasswordHash);
        }

        private static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, HashIterations, HashAlgorithmName.SHA256, HashSize);
            return $"{HashIterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        private static bool VerifyPassword(string password, string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return false;
            string[] parts = stored.Split('.');
            if (parts.Length != 3 || !int.TryParse(parts[0], out int iterations) || iterations <= 0)
                return false;

            try
            {
                byte[] salt = Convert.FromBase64String(parts[1]);
                byte[] expected = Convert.FromBase64String(parts[2]);
                if (salt.Length == 0 || expected.Length == 0)
                    return false;
                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
