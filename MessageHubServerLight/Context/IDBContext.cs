using System.Data;

namespace MessageHubServerLight.Context;

public interface IDBContext
{
    IDbConnection CreateConnection();
}