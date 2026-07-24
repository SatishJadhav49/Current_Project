
namespace API_LSP.Data
{
    public class MM_DashboardDataService
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MM_DashboardDataService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

    }
}
