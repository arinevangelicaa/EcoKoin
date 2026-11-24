namespace EcoKoin.Services
{
    /// <summary>
    /// Thrown by <see cref="IAppRepository.SaveUserAsync"/> when the username is already used by another user.
    /// Provider-neutral replacement for a database-specific unique-constraint error.
    /// </summary>
    public sealed class DuplicateUsernameException : Exception
    {
        public DuplicateUsernameException(string username, Exception? innerException = null)
            : base($"Username '{username}' is already taken.", innerException)
        {
            Username = username;
        }

        public string Username { get; }
    }
}
