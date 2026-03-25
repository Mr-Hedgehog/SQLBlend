# SQLBlend

English version: `README.md`

***SQLBlend*** — консольное C#-приложение для выполнения SQL-запросов к разным базам данных (сейчас поддерживаются ***Microsoft SQL Server*** и ***PostgreSQL***), объединения результатов и применения операций фильтрации/агрегации. Приложение полностью настраивается через конфиг: источники данных, запросы, параметры и операции преобразования.

## Возможности
* Поддержка нескольких БД: SQL Server и PostgreSQL.
* Конфигурация через JSON (`config.json`):
  * несколько строк подключения;
  * набор запросов с привязкой к источникам данных и SQL-файлам;
  * параметры, использующие результаты предыдущих запросов.
* Последовательное выполнение запросов: результаты одного запроса можно подставлять в следующий (например, для `IN`-списка).
* Агрегации и фильтрация в памяти:
  * `Union` — объединение результатов;
  * `Filter` — фильтрация строк по условию;
  * `InnerJoin` и `LeftJoin` — join между результатами разных запросов.

## Сценарий работы
### 1. Подготовьте `config.json`
Пример:
```json
{
  "Description": "Необязательное описание конфигурации",
  "ConnectionStrings": [
    {
      "Name": "Source1",
      "Type": "SqlServer",
      "ConnectionString": "Server=...;Database=...;User Id=...;Password=..."
    },
    {
      "Name": "Source2",
      "Type": "Postgres",
      "ConnectionString": "Host=...;Database=...;Username=...;Password=..."
    }
  ],
  "Queries": [
    {
      "Name": "Query1",
      "OutputFileName": "query1_result",
      "DataSourceName": "Source1",
      "QueryFilePath": "queries/query1.sql"
    },
    {
      "Name": "Query2",
      "DataSourceName": "Source2",
      "QueryFilePath": "queries/query2.sql",
      "Parameters": [
        {
          "Name": "PrevIds",
          "FromQuery": "Query1",
          "Column": "Id",
          "Format": "InClause"
        }
      ]
    }
  ],
  "FiltersAndAggregations": [
    {
      "Name": "FinalResult",
      "OutputFileName": "final_result",
      "Operations": [
        {
          "Operation": "Union",
          "QueryNames": ["Query1", "Query2"]
        },
        {
          "Operation": "Filter",
          "Condition": "Value > 100"
        }
      ]
    }
  ]
}
```

### `config.override.json` рядом с `.exe`
Можно создать `config.override.json` рядом с исполняемым файлом, чтобы переопределять строки подключения без изменения основного `config.json`.

Пример:
```json
{
  "ConnectionStrings": [
    {
      "Name": "Source1",
      "ConnectionString": "Server=localhost;Database=...;User Id=...;Password=..."
    }
  ]
}
```

Если `config.override.json` найден, строки подключения переопределяются по совпадению `Name`.

### Описание полей
* `Description` — необязательное описание конфигурации (показывается в селекторе конфигов).
* `OutputFileName` — необязательное имя выходного CSV (без расширения). Если не задано, используется `Name`.

## Примеры операций

### 1) `Union`
```json
{
  "Operation": "Union",
  "QueryNames": ["Query1", "Query2", "Query3"]
}
```

### 2) `Filter`
`Filter` применяется к текущему промежуточному результату.
```json
{
  "Operation": "Filter",
  "Condition": "Value >= 100"
}
```

Поддерживаемые операторы: `=`, `!=`, `<>`, `>`, `<`, `>=`, `<=`, `LIKE`, `IN`.

Примеры условий:
* `"Condition": "Status = Active"`
* `"Condition": "Name LIKE %test%"`
* `"Condition": "Type IN A,B,C"`

### 3) `InnerJoin`
```json
{
  "Operation": "InnerJoin",
  "LeftQueryName": "Orders",
  "RightQueryName": "Customers",
  "JoinConditions": [
    {
      "LeftColumn": "CustomerId",
      "RightColumn": "Id",
      "Operator": "="
    }
  ],
  "SelectColumns": [
    { "Query": "Left", "Column": "OrderId" },
    { "Query": "Left", "Column": "Amount" },
    { "Query": "Right", "Column": "Name" }
  ]
}
```

### 4) `LeftJoin`
```json
{
  "Operation": "LeftJoin",
  "LeftQueryName": "Orders",
  "RightQueryName": "Customers",
  "JoinConditions": [
    {
      "LeftColumn": "CustomerId",
      "RightColumn": "Id",
      "Operator": "="
    }
  ],
  "SelectColumns": [
    { "Query": "Left", "Column": "OrderId" },
    { "Query": "Left", "Column": "CustomerId" },
    { "Query": "Right", "Column": "Name" }
  ]
}
```

> Важно: для join сейчас поддерживается только `"Operator": "="`.

## Переменные SQL на уровне запроса

Можно задавать `Variables` для конкретного запроса и использовать их в SQL через шаблоны `{{VariableName}}`.

Пример в конфиге:
```json
{
  "Name": "query1",
  "DataSourceName": "Query1Source",
  "QueryFilePath": "queries/query1.sql",
  "Variables": {
    "PeriodStartDate": "2026-01-01"
  }
}
```

Пример SQL (`queries/query1.sql`):
```sql
SELECT *
FROM documents
WHERE created_at >= '{{PeriodStartDate}}';
```

## 2. Добавьте SQL-файлы
Примеры:

`queries/query1.sql`
```sql
SELECT Id, Name, Value FROM SomeTable;
```

`queries/query2.sql`
```sql
SELECT Id, Name, Value FROM AnotherTable WHERE Id IN @PrevIds;
```

## 3. Запустите приложение
Приложение:
* загрузит конфигурацию;
* подключится к указанным БД;
* выполнит запросы по порядку;
* применит операции фильтрации/агрегации;
* сохранит результаты в CSV.

## 4. Аргументы командной строки

Поддерживаются опциональные аргументы:

* `--cache`  
  Включает использование кэша (чтение ранее сохранённых результатов из папки `Results`).

* `<configsBaseDir>`  
  Путь к базовой директории с папками конфигураций.  
  Если не указан, используется текущая рабочая директория.

Поведение по умолчанию (без `--cache`):
* кэш выключен;
* файлы в `Results` очищаются перед запуском;
* запросы и агрегации выполняются заново.

Примеры:
* `SQLBlend.exe`
* `SQLBlend.exe --cache`
* `SQLBlend.exe C:\Configs`
* `SQLBlend.exe C:\Configs --cache`

## Требования
* .NET 9.0 или новее
* Dapper
* `Microsoft.Data.SqlClient` (SQL Server)
* `Npgsql` (PostgreSQL)

## Участие в разработке
Будем рады pull request'ам и issue с предложениями/исправлениями.
