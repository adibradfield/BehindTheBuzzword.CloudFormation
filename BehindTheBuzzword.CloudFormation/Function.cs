using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using BehindTheBuzzword.CloudFormation.DTO;
using MySql.Data.MySqlClient;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace BehindTheBuzzword.CloudFormation
{
    public class Functions
    {
        private readonly string _host = Environment.GetEnvironmentVariable("ServerlessDeployment_MySQL__Host");
        private readonly string _user = Environment.GetEnvironmentVariable("ServerlessDeployment_MySQL__User");
        private readonly string _password = Environment.GetEnvironmentVariable("ServerlessDeployment_MySQL__Password");

        private string ConnectionString => $"Server={_host};Database=BehindTheBuzzword;Uid={_user};Pwd={_password};Connect Timeout=180";

        public async Task DatabaseTableCustomResource(CustomResourceRequest<DatabaseTableRequestData> request, ILambdaContext context)
        {
            var physicalResourceId = "unassigned-physical-resource-id";

            try
            {
                context.Logger.LogLine(JsonSerializer.Serialize(request));

                DatabaseTableResponseData response = new DatabaseTableResponseData{Name = request.ResourceProperties.Name};

                switch (request.RequestType)
                {
                    case "Create":
                        physicalResourceId = $"{request.StackId}/{request.ResourceType}/{request.LogicalResourceId}";
                        await CreateTable(request.ResourceProperties);
                        break;
                    case "Update":
                        physicalResourceId = request.PhysicalResourceId;
                        await UpdateTable(request.OldResourceProperties, request.ResourceProperties);
                        break;
                    case "Delete":
                        physicalResourceId = request.PhysicalResourceId;
                        await DropTable(request.ResourceProperties);
                        break;
                    default:
                        throw new NotSupportedException($"Cannot handle RequestType {request.RequestType}");
                }

                await RespondToCloudFormation(request, physicalResourceId, response);
            }
            catch (Exception ex)
            {
                await RespondToCloudFormation(request, physicalResourceId, ex);
                LambdaLogger.Log("Exception: " + ex);
            }
        }

        private async Task RespondToCloudFormation(CustomResourceRequest request, string physicalResourceId, Exception exception = null)
        {
            await RespondToCloudFormation<object>(request, physicalResourceId, exception: exception);
        }

        private async Task RespondToCloudFormation<T>(CustomResourceRequest request, string physicalResourceId, T data = null, Exception exception = null) where T : class
        {
            var response = new CustomResourceResponse<T>
            {
                Status = exception != null ? "FAILED" : "SUCCESS",
                Reason = exception?.Message ?? string.Empty,
                PhysicalResourceId = physicalResourceId,
                StackId = request.StackId,
                RequestId = request.RequestId,
                LogicalResourceId = request.LogicalResourceId,
                Data = data
            };

            try
            {
                var client = new HttpClient();

                var body = JsonSerializer.Serialize(response);
                var jsonContent = new StringContent(body);
                jsonContent.Headers.Remove("Content-Type");

                LambdaLogger.Log(body);

                var postResponse = await client.PutAsync(request.ResponseURL, jsonContent);

                postResponse.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                LambdaLogger.Log("Exception: " + ex);
            }
        }

        #region Database Update Methods

        private async Task CreateTable(DatabaseTableRequestData tableData)
        {
            var columnDefinitions = tableData.Columns.Select(column =>
                $"{column.Key} {column.Value.DataType}{(column.Value.IsAutoIncrement ? " AUTO_INCREMENT" : null)}");

            var primaryKeyColumns = tableData.Columns.Where(kvp => kvp.Value.IsPrimaryKey).Select(kvp => kvp.Key).ToArray();

            var sql = $"CREATE TABLE {tableData.Name}(";
            sql += string.Join(',', columnDefinitions);

            if (primaryKeyColumns.Any())
            {
                var primaryKeyDefinition = string.Join(',', primaryKeyColumns);
                sql += $",PRIMARY KEY({primaryKeyDefinition})";
            }

            sql += ");";

            LambdaLogger.Log($"Executing SQL: [{sql}]");

            using (var connection = new MySqlConnection(ConnectionString))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
            }
        }

        private async Task DropTable(DatabaseTableRequestData tableData)
        {
            var sql = $"DROP TABLE IF EXISTS {tableData.Name}";

            LambdaLogger.Log($"Executing SQL: [{sql}]");

            using (var connection = new MySqlConnection(ConnectionString))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
            }
        }

        private async Task UpdateTable(DatabaseTableRequestData oldTable, DatabaseTableRequestData newTable)
        {
            var updateColumns = newTable.Columns.Keys.Where(k => oldTable.Columns.Keys.Contains(k) && 
                                                                 (oldTable.Columns[k].DataType != newTable.Columns[k].DataType ||
                                                                  oldTable.Columns[k].IsAutoIncrement != newTable.Columns[k].IsAutoIncrement));
            var newColumns = newTable.Columns.Keys.Where(k => !oldTable.Columns.Keys.Contains(k));
            var deletedColumns = oldTable.Columns.Keys.Where(k => !newTable.Columns.Keys.Contains(k));

            var newColumnDefinitions = newColumns.Select(k => $"ADD COLUMN {k} {newTable.Columns[k].DataType}{(newTable.Columns[k].IsAutoIncrement ? " AUTO_INCREMENT" : null)}");
            var deletedColumnDefinitions = deletedColumns.Select(k => $"DROP COLUMN {k}");
            var updateColumnDefinitions = updateColumns.Select(k => $"MODIFY COLUMN {k} {newTable.Columns[k].DataType}{(newTable.Columns[k].IsAutoIncrement ? " AUTO_INCREMENT" : null)}");

            var alterDefinitions = new List<string>(deletedColumnDefinitions.Concat(newColumnDefinitions).Concat(updateColumnDefinitions));

            if (oldTable.Name != newTable.Name)
            {
                alterDefinitions.Add($"RENAME TO {newTable.Name}");
            }

            var sql = $"ALTER TABLE {oldTable.Name} {string.Join(',', alterDefinitions)};";

            LambdaLogger.Log($"Executing SQL: [{sql}]");

            using (var connection = new MySqlConnection(ConnectionString))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
            }
        }

        #endregion
    }
}
