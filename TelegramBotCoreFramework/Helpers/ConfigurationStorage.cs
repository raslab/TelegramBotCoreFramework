using Google.Cloud.Firestore;

namespace Helpers;

public class ConfigurationStorage
{
    private FirestoreDb _firestoreDb;
    private const string ConfigsCollection = "configs";
    private const string IndexerDocument = "_indexers";

    public ConfigurationStorage(FirestoreDb firestoreDb)
    {
        this._firestoreDb = firestoreDb;
    }

    public async Task<T?> Get<T>() 
    {
        var documentReference = _firestoreDb.Collection(ConfigsCollection).Document(typeof(T).Name);
        DocumentSnapshot documentSnapshot = await documentReference.GetSnapshotAsync();

        if (!documentSnapshot.Exists)
        {
            return default;
        }

        return documentSnapshot.ConvertTo<T>();
    }

    public async Task Push<T>(T obj)
    {
        var documentReference = _firestoreDb.Collection(ConfigsCollection).Document(typeof(T).Name);
        await documentReference.SetAsync(obj, SetOptions.Overwrite);
    }

    public async Task<long> GetAndIncIndexer(string indexerName)
    {
        var documentReference = _firestoreDb.Collection(ConfigsCollection).Document(IndexerDocument);
        DocumentSnapshot documentSnapshot = await documentReference.GetSnapshotAsync();

        long index = 0;
        
        if (documentSnapshot.Exists)
        {
            documentSnapshot.TryGetValue<long>(indexerName, out index);
            // Increment index
            index++;
            // Write back to property
            await documentReference.UpdateAsync(indexerName, index);
        }
        else
        {
            index = 0;
            // Create the document with the incremented index
            await documentReference.SetAsync(new Dictionary<string, object>
            {
                { indexerName, index + 1 }
            });
            index++;
        }
        
        return index;
    }

    public async Task Remove<T>()
    {
        var documentReference = _firestoreDb.Collection(ConfigsCollection).Document(typeof(T).Name);
        var snapshot = await documentReference.GetSnapshotAsync();
        if (snapshot.Exists)
            await documentReference.DeleteAsync();
    }
}