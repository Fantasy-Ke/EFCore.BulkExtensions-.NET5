﻿using EFCore.BulkExtensions.SQLAdapters;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EFCore.BulkExtensions.SQLAdapters.Oracle
{
    public class SqlQueryBuilderOracle : QueryBuilderExtensions
    {
        /// <summary>
        /// 生成SQL查询以创建表副本
        /// </summary>
        /// <param name="existingTableName"></param>
        /// <param name="newTableName"></param>
        /// <param name="useTempDb"></param>
        /// <returns></returns>
        public static string CreateTableCopy(string existingTableName, string newTableName, bool useTempDb)
        {
            string keywordTemp = useTempDb ? "GLOBAL TEMPORARY " : "";
            string typeTemp = useTempDb ? " ON COMMIT PRESERVE ROWS " : "";
            var query = $"CREATE {keywordTemp}TABLE {newTableName} {typeTemp}" +
                    $"AS SELECT* FROM {existingTableName} WHERE 1 = 2;";
            query = query.Replace("[", "").Replace("]", "");
            return query;
        }

        /// <summary>
        /// 生成SQL查询以删除表
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="isTempTable"></param>
        /// <returns></returns>
        public static string DropTable(string tableName, bool isTempTable)
        {
            string query;

            if (isTempTable)
            {
                query = $@"BEGIN 
                    EXECUTE IMMEDIATE 'TRUNCATE TABLE {tableName}'; 
                    EXECUTE IMMEDIATE 'DROP TABLE {tableName} PURGE'; 
                    EXCEPTION WHEN OTHERS THEN NULL;
                   END;";
            }
            else
            {
                query = $@"BEGIN 
                    EXECUTE IMMEDIATE 'DROP TABLE {tableName}'; 
                    EXCEPTION WHEN OTHERS THEN NULL;
                   END;";
            }

            query = query.Replace("[", "").Replace("]", "");

            return query;
        }

        /// <summary>
        /// 返回给定表的列列表
        /// </summary>
        /// <param name="tableInfo"></param>
        /// <param name="operationType"></param>
        public static List<string> GetColumnList(TableInfo tableInfo, OperationType operationType)
        {
            var tempDict = tableInfo.PropertyColumnNamesDict;
            if (operationType == OperationType.Insert && tableInfo.PropertyColumnNamesDict.Any()) // Only OnInsert omit colums with Default values
            {
                tableInfo.PropertyColumnNamesDict = tableInfo.PropertyColumnNamesDict.Where(a => !tableInfo.DefaultValueProperties.Contains(a.Key)).ToDictionary(a => a.Key, a => a.Value);
            }

            List<string> columnsList = tableInfo.PropertyColumnNamesDict.Values.ToList();
            List<string> propertiesList = tableInfo.PropertyColumnNamesDict.Keys.ToList();

            tableInfo.PropertyColumnNamesDict = tempDict;

            //bool keepIdentity = tableInfo.BulkConfig.SqlBulkCopyOptions.HasFlag(OracleBulkCopyOptions.Default);
            //var uniquColumnName = tableInfo.PrimaryKeysPropertyColumnNameDict.Values.ToList().FirstOrDefault();
            //if (!keepIdentity && tableInfo.HasIdentity && (operationType == OperationType.Insert || tableInfo.IdentityColumnName != uniquColumnName))
            //{
            //    var identityPropertyName = tableInfo.PropertyColumnNamesDict.SingleOrDefault(a => a.Value == tableInfo.IdentityColumnName).Key;
            //    columnsList = columnsList.Where(a => a != tableInfo.IdentityColumnName).ToList();
            //    propertiesList = propertiesList.Where(a => a != identityPropertyName).ToList();
            //}

            return columnsList;
        }

        /// <summary>
        /// 生成SQL合并语句
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tableInfo"></param>
        /// <param name="operationType"></param>
        /// <exception cref="NotImplementedException"></exception>
        public static string MergeTable<T>(TableInfo tableInfo, OperationType operationType) where T : class
        {
            List<string> columnsNames = tableInfo.PropertyColumnNamesDict.Values.ToList();
            List<string> nonIdentityColumnsNames = columnsNames.Where(a => !a.Equals(tableInfo.IdentityColumnName, StringComparison.OrdinalIgnoreCase)).ToList();
            List<string> columnsNamesOnUpdate = tableInfo.PropertyColumnNamesUpdateDict.Values.ToList();
            List<string> columnsNamesOnCompare = tableInfo.PropertyColumnNamesCompareDict.Values.ToList();
            List<string> primaryKeys = tableInfo.PrimaryKeysPropertyColumnNameDict.Where(a => tableInfo.PropertyColumnNamesDict.ContainsKey(a.Key)).Select(a => a.Value).ToList();
            List<string> updateColumnNames = columnsNamesOnUpdate.Where(a => !a.Equals(tableInfo.IdentityColumnName, StringComparison.OrdinalIgnoreCase)).ToList();
            List<string> compareColumnNames = columnsNamesOnCompare.Where(a => !a.Equals(tableInfo.IdentityColumnName, StringComparison.OrdinalIgnoreCase)).ToList();
            List<string> insertColumnsNames = nonIdentityColumnsNames;
            List<string> outputColumnsNames = tableInfo.OutputPropertyColumnNamesDict.Values.ToList();
            string targetTable = tableInfo.FullTableName;
            string sourceTable = tableInfo.FullTempTableName;
            
            string isUpdateStatsValue = (tableInfo.BulkConfig.CalculateStats)
                ? ",(CASE $action WHEN 'UPDATE' THEN 1 Else 0 END),(CASE $action WHEN 'DELETE' THEN 1 Else 0 END)"
                : string.Empty;
            string query;
            var firstPrimaryKey = tableInfo.PrimaryKeysPropertyColumnNameDict.FirstOrDefault().Key;
            if (operationType == OperationType.Delete)
            {
                query = $"delete FROM {tableInfo.FullTableName} WHERE " +
                        $"{firstPrimaryKey} IN (SELECT {firstPrimaryKey} FROM {tableInfo.FullTempTableName}); ";
            }
            else
            {
                query = $"MERGE INTO {targetTable} T " +
                    $"USING {sourceTable} S " +
                    $"ON ({GetANDSeparatedColumns(primaryKeys, "T", "S", tableInfo.UpdateByPropertiesAreNullable)})";
                query += (primaryKeys.Count == 0) ? "1=0" : string.Empty;

                if (operationType == OperationType.Insert || operationType == OperationType.InsertOrUpdate)
                {
                    query += $" WHEN NOT MATCHED " +
                         $"THEN INSERT ({GetCommaSeparatedColumns(insertColumnsNames)})" +
                         $" VALUES ({GetCommaSeparatedColumns(insertColumnsNames, "S")})";
                }

                if (operationType == OperationType.Update || operationType == OperationType.InsertOrUpdate)
                {
                    if (updateColumnNames.Count == 0 && operationType == OperationType.Update)
                    {
                        throw new InvalidBulkConfigException($"'Bulk{operationType}' operation can not have zero columns to update.");
                    }
                    else if (updateColumnNames.Count > 0)
                    {
                        query += $" WHEN MATCHED " +
                            $" THEN UPDATE SET {GetCommaSeparatedColumns(updateColumnNames, "T", "S")}";
                    }
                }

                query = query.Replace("INSERT () VALUES ()", "INSERT DEFAULT VALUES");
                if (tableInfo.CreatedOutputTable)
                {
                    string commaSeparatedColumnsNames = GetCommaSeparatedColumns(outputColumnsNames, "INSERTED");
                    query += $" OUTPUT {commaSeparatedColumnsNames}" + isUpdateStatsValue +
                         $" INTO {tableInfo.FullTempOutputTableName}";
                }
                query += ";";
            }

            query = query.Replace("[", "").Replace("]", "");

            Dictionary<string, string>? sourceDestinationMappings = tableInfo.BulkConfig.CustomSourceDestinationMappingColumns;
            if (tableInfo.BulkConfig.CustomSourceTableName != null && sourceDestinationMappings != null && sourceDestinationMappings.Count > 0)
            {
                var textSelect = "SELECT ";
                var textFrom = " FROM";
                int startIndex = query.IndexOf(textSelect);
                var qSegment = query[startIndex..query.IndexOf(textFrom)];
                var qSegmentUpdated = qSegment;
                foreach (var mapping in sourceDestinationMappings)
                {
                    var propertyFormated = $"{mapping.Value}";
                    var sourceProperty = mapping.Key;

                    if (qSegment.Contains(propertyFormated))
                    {
                        qSegmentUpdated = qSegmentUpdated.Replace(propertyFormated, $"{sourceProperty}");
                    }
                }
                if (qSegment != qSegmentUpdated)
                {
                    query = query.Replace(qSegment, qSegmentUpdated);
                }
            }
            return query;
        }

        /// <summary>
        /// 生成SQL查询以从表中选择输出
        /// </summary>
        /// <param name="tableInfo"></param>
        /// <returns></returns>
        public override string SelectFromOutputTable(TableInfo tableInfo)
        {
            List<string> columnsNames = tableInfo.OutputPropertyColumnNamesDict.Values.ToList();
            var query = $"SELECT {SqlQueryBuilder.GetCommaSeparatedColumns(columnsNames)} FROM {tableInfo.FullTempOutputTableName} WHERE [{tableInfo.PrimaryKeysPropertyColumnNameDict.Select(x => x.Value).FirstOrDefault()}] IS NOT NULL";
            query = query.Replace("[", "").Replace("]", "");
            return query;
        }

        /// <summary>
        /// 生成SQL查询以创建唯一约束
        /// </summary>
        /// <param name="tableInfo"></param>
        public static string CreateUniqueConstrain(TableInfo tableInfo)
        {
            var tableName = tableInfo.TableName;
            var schemaFormated = tableInfo.Schema == null ? "" : $@"`{tableInfo.Schema}`.";
            var fullTableNameFormated = $@"{schemaFormated}`{tableName}`";

            var uniqueColumnNames = tableInfo.PrimaryKeysPropertyColumnNameDict.Values.ToList();
            var uniqueColumnNamesDash = string.Join("_", uniqueColumnNames);
            var schemaDash = tableInfo.Schema == null ? "" : $"{tableInfo.Schema}_";
            var uniqueConstrainName = $"tempUniqueIndex_{schemaDash}{tableName}_{uniqueColumnNamesDash}";

            var uniqueColumnNamesComma = string.Join(",", uniqueColumnNames); // TODO When Column is string without defined max length, it should be UNIQUE (`Name`(255)); otherwise exception: BLOB/TEXT column 'Name' used in key specification without a key length'
            uniqueColumnNamesComma = "`" + uniqueColumnNamesComma;
            uniqueColumnNamesComma = uniqueColumnNamesComma.Replace(",", "`, `");
            var uniqueColumnNamesFormated = uniqueColumnNamesComma.TrimEnd(',');
            uniqueColumnNamesFormated = uniqueColumnNamesFormated + "`";

            var q = $@"ALTER TABLE {fullTableNameFormated} " +
                    $@"ADD CONSTRAINT `{uniqueConstrainName}` " +
                    $@"UNIQUE ({uniqueColumnNamesFormated})";
            return q;
        }

        /// <summary>
        /// 生成SQL查询以删除唯一约束
        /// </summary>
        /// <param name="tableInfo"></param>
        public static string DropUniqueConstrain(TableInfo tableInfo)
        {
            var tableName = tableInfo.TableName;
            var schemaFormated = tableInfo.Schema == null ? "" : $@"`{tableInfo.Schema}`.";
            var fullTableNameFormated = $@"{schemaFormated}`{tableName}`";

            var uniqueColumnNames = tableInfo.PrimaryKeysPropertyColumnNameDict.Values.ToList();
            var uniqueColumnNamesDash = string.Join("_", uniqueColumnNames);
            var schemaDash = tableInfo.Schema == null ? "" : $"{tableInfo.Schema}_";
            var uniqueConstrainName = $"tempUniqueIndex_{schemaDash}{tableName}_{uniqueColumnNamesDash}";

            var q = $@"DROP INDEX `{uniqueConstrainName}`;";
            return q;
        }

        /// <summary>
        /// 如果存在唯一约束，则生成SQL查询以检查
        /// </summary>
        /// <param name="tableInfo"></param>
        public static string HasUniqueConstrain(TableInfo tableInfo)
        {
            var tableName = tableInfo.TableName;

            var uniqueColumnNames = tableInfo.PrimaryKeysPropertyColumnNameDict.Values.ToList();
            var uniqueColumnNamesDash = string.Join("_", uniqueColumnNames);
            var schemaDash = tableInfo.Schema == null ? "" : $"{tableInfo.Schema}_";
            var uniqueConstrainName = $"tempUniqueIndex_{schemaDash}{tableName}_{uniqueColumnNamesDash}";

            var q = $@"SELECT DISTINCT CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE " +
                    $@"CONSTRAINT_TYPE = 'UNIQUE' AND CONSTRAINT_NAME = '{uniqueConstrainName}';";
            return q;
        }

        /// <summary>
        /// 为批处理命令重新构造sql查询
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="isDelete"></param>
        public override string RestructureForBatch(string sql, bool isDelete = false)
        {
            return sql;
        }

        /// <summary>
        /// 返回按提供程序无形的DbParameters
        /// </summary>
        /// <param name="sqlParameter"></param>
        /// <returns></returns>
        public override object CreateParameter(Microsoft.Data.SqlClient.SqlParameter sqlParameter)
        {
            return sqlParameter;
        }

        /// <inheritdoc/>
        public override object Dbtype()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void SetDbTypeParam(object npgsqlParameter, object dbType)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Generates SQL query to add a column
        /// </summary>
        /// <param name="fullTableName"></param>
        /// <param name="columnName"></param>
        /// <param name="columnType"></param>
        /// <returns></returns>
        public static string AddColumn(string fullTableName, string columnName, string columnType)
        {
            var q = $"ALTER TABLE {fullTableName} ADD [{columnName}] {columnType};";
            return q;
        }

        /// <summary>
        /// Generates SQL query to join table
        /// </summary>
        /// <param name="tableInfo"></param>
        /// <returns></returns>
        public static string SelectJoinTable(TableInfo tableInfo)
        {
            string sourceTable = tableInfo.FullTableName;
            string joinTable = tableInfo.FullTempTableName;
            List<string> columnsNames = tableInfo.PropertyColumnNamesDict.Values.ToList();
            List<string> selectByPropertyNames = tableInfo.PropertyColumnNamesDict.Where(a => tableInfo.PrimaryKeysPropertyColumnNameDict.ContainsKey(a.Key)).Select(a => a.Value).ToList();

            var q = $"SELECT {GetCommaSeparatedColumns(columnsNames, "S")} FROM {sourceTable} AS S " +
                    $"JOIN {joinTable} AS J " +
                    $"ON {GetANDSeparatedColumns(selectByPropertyNames, "S", "J", tableInfo.UpdateByPropertiesAreNullable)}";
            return q;
        }

        /// <summary>
        /// Generates SQL query to truncate table
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public static string TruncateTable(string tableName)
        {
            var q = $"TRUNCATE TABLE {tableName};";
            return q;
        }

        // propertColumnsNamesDict used with Sqlite for @parameter to be save from non valid charaters ('', '!', ...) that are allowed as column Names in Sqlite

        /// <summary>
        /// Generates SQL query to get comma seperated column
        /// </summary>
        /// <param name="columnsNames"></param>
        /// <param name="prefixTable"></param>
        /// <param name="equalsTable"></param>
        /// <param name="propertColumnsNamesDict"></param>
        /// <returns></returns>
        public static string GetCommaSeparatedColumns(List<string> columnsNames, string? prefixTable = null, string? equalsTable = null, Dictionary<string, string>? propertColumnsNamesDict = null)
        {
            prefixTable += (prefixTable != null && prefixTable != "@") ? "." : "";
            equalsTable += (equalsTable != null && equalsTable != "@") ? "." : "";

            string commaSeparatedColumns = "";
            foreach (var columnName in columnsNames)
            {
                var equalsParameter = propertColumnsNamesDict == null ? columnName : propertColumnsNamesDict.SingleOrDefault(a => a.Value == columnName).Key;
                commaSeparatedColumns += prefixTable != "" ? $"{prefixTable}[{columnName}]" : $"[{columnName}]";
                commaSeparatedColumns += equalsTable != "" ? $" = {equalsTable}[{equalsParameter}]" : "";
                commaSeparatedColumns += ", ";
            }
            if (commaSeparatedColumns != "")
            {
                commaSeparatedColumns = commaSeparatedColumns.Remove(commaSeparatedColumns.Length - 2, 2); // removes last excess comma and space: ", "
            }
            return commaSeparatedColumns;
        }

        /// <summary>
        /// Generates SQL query to alter table columns to nullables
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="tableInfo"></param>
        /// <returns></returns>
        public static string AlterTableColumnsToNullable(string tableName, TableInfo tableInfo)
        {
            string q = "";
            foreach (var column in tableInfo.ColumnNamesTypesDict)
            {
                string columnName = column.Key;
                string columnType = column.Value;
                if (columnName == tableInfo.TimeStampColumnName)
                    columnType = tableInfo.TimeStampOutColumnType;
                q += $"ALTER TABLE {tableName} ALTER COLUMN [{columnName}] {columnType}; ";
            }
            return q;
        }

        /// <summary>
        /// Generates SQL query to seperate columns
        /// </summary>
        /// <param name="columnsNames"></param>
        /// <param name="prefixTable"></param>
        /// <param name="equalsTable"></param>
        /// <param name="updateByPropertiesAreNullable"></param>
        /// <param name="propertColumnsNamesDict"></param>
        /// <returns></returns>
        public static string GetANDSeparatedColumns(List<string> columnsNames, string? prefixTable = null, string? equalsTable = null, bool updateByPropertiesAreNullable = false, Dictionary<string, string>? propertColumnsNamesDict = null)
        {
            string commaSeparatedColumns = GetCommaSeparatedColumns(columnsNames, prefixTable, equalsTable, propertColumnsNamesDict);

            if (updateByPropertiesAreNullable)
            {
                string[] columns = commaSeparatedColumns.Split(',');
                string commaSeparatedColumnsNullable = String.Empty;
                foreach (var column in columns)
                {
                    string[] columnTS = column.Split('=');
                    string columnT = columnTS[0].Trim();
                    string columnS = columnTS[1].Trim();
                    string columnNullable = $"({column.Trim()} OR ({columnT} IS NULL AND {columnS} IS NULL))";
                    commaSeparatedColumnsNullable += columnNullable + ", ";
                }
                if (commaSeparatedColumns != "")
                {
                    commaSeparatedColumnsNullable = commaSeparatedColumnsNullable.Remove(commaSeparatedColumnsNullable.Length - 2, 2);
                }
                commaSeparatedColumns = commaSeparatedColumnsNullable;
            }

            string ANDSeparatedColumns = commaSeparatedColumns.Replace(",", " AND");
            return ANDSeparatedColumns;
        }
    }

}

/// <summary>
///  包含生成EFCore所需SQL查询的方法列表
/// </summary>
