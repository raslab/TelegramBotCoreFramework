using Google.Cloud.Firestore;

namespace Helpers;

public class FirestoreRepository<T>
{
    private readonly CollectionReference _collection;

    public FirestoreRepository(FirestoreDb db, string collectionName)
    {
        _collection = db.Collection(collectionName);
    }

    public CollectionReference Collection => _collection;

    // public async Task AddAsync(T item)
    // {
    //     await _collection.AddAsync(item);
    // }

    public async Task DeleteAsync(string id)
    {
        await _collection.Document(id).DeleteAsync();
    }

    public async Task<T> GetAsync(string id)
    {
        DocumentSnapshot snapshot = await _collection.Document(id).GetSnapshotAsync();
        return snapshot.ConvertTo<T>();
    }
    
    public async Task<(string, T)[]> GetAllAsync()
    {
        QuerySnapshot snapshot = await _collection.GetSnapshotAsync();
        return snapshot.Documents.Select(doc => (doc.Id, doc.ConvertTo<T>())).ToArray();
    }

    public async Task<string[]> GetAllIdsAsync()
    {
        QuerySnapshot snapshot = await _collection.GetSnapshotAsync();
        return snapshot.Documents.Select(doc => doc.Id).ToArray();
    }

    public Task UpdateAsync(string id, T item)
    {
        DocumentReference documentReference = _collection.Document(id);
        return documentReference.SetAsync(item, SetOptions.Overwrite);
    }
}