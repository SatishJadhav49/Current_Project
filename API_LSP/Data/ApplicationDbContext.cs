using Microsoft.Data.SqlClient;

namespace API_LSP.Data
{
    public interface IDbConnectionFactory
    {
        SqlConnection CreateConnection();
        Task<SqlConnection> CreateConnectionAsync();
        SqlConnection CreateShowerConnection();
        Task<SqlConnection> CreateShowerConnectionAsync();
    }

    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;
        private readonly string _showerConnectionString;

        public DbConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration), "Connection string not found");
            _showerConnectionString = configuration.GetConnectionString("Shower_Test_Connection")
                ?? throw new ArgumentNullException(nameof(configuration), "Shower_Test_Connection string not found");
        }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<SqlConnection> CreateConnectionAsync()
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }

        public SqlConnection CreateShowerConnection()
        {
            return new SqlConnection(_showerConnectionString);
        }

        public async Task<SqlConnection> CreateShowerConnectionAsync()
        {
            var connection = new SqlConnection(_showerConnectionString);
            await connection.OpenAsync();
            return connection;
        }

    }
}
