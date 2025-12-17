using System;
using Npgsql;
try
{
    var cs = "postgres://user:pass@localhost:5432/db";
    var builder = new NpgsqlConnectionStringBuilder(cs);
    Console.WriteLine(builder.ConnectionString);
}
catch (Exception ex)
{
    Console.WriteLine(ex.GetType().FullName);
    Console.WriteLine(ex.Message);
}
