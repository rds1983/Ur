# Ur

Ur is a lightweight persistence library for .NET that serializes objects that implement `IHasId<KeyType>` to JSON files. It offers three storage strategies:

- **MultipleFilesStorage** — each object is serialized to its own file, identified by its `Id`.
- **SingleFileStorage** — all objects of a given type are serialized into a single JSON array file.
- **CustomStorage** — a single standalone object (not keyed by `IHasId`) persisted to one file.

All storage types are registered with `UrContext`, which manages the data folder, logging, JSON options, and a two-phase load cycle: first all data is deserialized, then cross-storage references are resolved via `SetReferences()`.

## Basic Usage

```csharp
class Employee : IHasId<string>
{
    public string Id { get; set; }
    public string Name { get; set; }
}

// 1. Create a storage — each employee will be saved as {Id}.json
//    under the "employees" subfolder.
var storage = new MultipleFilesStorage<Employee>("employees");

// 2. Register with UrContext so it participates in Load/Clear cycles.
UrContext.Register(storage);

// 3. Point UrContext at a root data folder and load existing data.
UrContext.Load(@"C:\Data");

// 4. Create objects and persist them.
var alice = new Employee { Id = "alice", Name = "Alice" };
var bob = new Employee { Id = "bob", Name = "Bob" };
storage.Create(alice);
storage.Create(bob);
storage.SaveAll();

// 5. Retrieve by key.
var loaded = storage.EnsureByKey("alice");
Console.WriteLine(loaded.Name); // "Alice"

// 6. Clear in-memory cache and reload from disk.
UrContext.Clear();
UrContext.Load(@"C:\Data");
Console.WriteLine(storage.Count); // 2
```

The resulting file layout under `C:\Data`:

```
C:\Data\
└── employees\
    ├── alice.json
    └── bob.json
```

Each `.json` file contains the indented JSON representation of a single `Employee` object.

## Advanced Usage

When objects reference other persistable objects, Ur serialises the reference as the target's `Id` string. You restore the live references in a `SetReferences()` override after all storages have been loaded.

Given these classes:

```csharp
class Employee : IHasId<string>
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Job Job { get; set; }
}

class Job : IHasId<string>
{
    public string Id { get; set; }
    public string Name { get; set; }
}
```

Create a derived storage that resolves the `Job` reference after load:

```csharp
class EmployeeStorage : MultipleFilesStorage<Employee>
{
    public EmployeeStorage() : base("employees") { }

    protected override void SetReferences()
    {
        base.SetReferences();
        var jobStorage = (MultipleFilesStorage<Job>)UrContext.GetStorageByType<Job>();
        foreach (var emp in this)
        {
            emp.Job = jobStorage.EnsureByKey(emp.Job.Id);
        }
    }
}
```

Usage:

```csharp
// 1. Register both storages.
var employees = new EmployeeStorage();
var jobs = new MultipleFilesStorage<Job>("jobs");

UrContext.Register(employees);
UrContext.Register(jobs);

// 2. Load existing data.
UrContext.Load(@"C:\Data");

// 3. Create jobs.
var junior = new Job { Id = "junior", Name = "Junior Developer" };
var senior = new Job { Id = "senior", Name = "Senior Developer" };
jobs.Create(junior);
jobs.Create(senior);

// 4. Create employees referencing those jobs.
var alice = new Employee { Id = "alice", Name = "Alice", Job = junior };
var bob   = new Employee { Id = "bob",   Name = "Bob",   Job = junior };
var charlie = new Employee { Id = "charlie", Name = "Charlie", Job = senior };

employees.Create(alice);
employees.Create(bob);
employees.Create(charlie);

// 5. Persist all.
jobs.SaveAll();
employees.SaveAll();
```

Serialized as JSON, the `Job` property of each `Employee` is written as the job's `Id` string (thanks to `UrConvertersFactory`):

```json
{
  "Id": "alice",
  "Name": "Alice",
  "Job": "junior"
}
```

After loading, `SetReferences()` replaces the string placeholder with the live `Job` object from the jobs storage.

File layout:

```
C:\Data\
├── employees\
│   ├── alice.json
│   ├── bob.json
│   └── charlie.json
└── jobs\
    ├── junior.json
    └── senior.json
```
