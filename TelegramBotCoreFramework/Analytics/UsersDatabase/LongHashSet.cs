using Google.Cloud.Firestore;
using Helpers;
using ProtoBuf;
using TL;

namespace Analytics.UsersDatabase;

[ProtoContract]
public class LongHashSet
{
    [ProtoMember(1)]
    public Dictionary<long, LongHashSet>? Baskets { get; set; } = null;
    
    [ProtoMember(2)]
    public bool HaveNumber { get; set; } = false;

    public int SubBasketsCount = 10; 

    public void Add(long val)
    {
        if (val < SubBasketsCount)
        {
            if (Baskets == null) Baskets = new Dictionary<long, LongHashSet>();
            if (Baskets.ContainsKey(val))
                Baskets[val].HaveNumber = true;
            else
                Baskets.Add(val, new LongHashSet() { HaveNumber = true });
        }
        else
        {
            if (Baskets == null) Baskets = new Dictionary<long, LongHashSet>();
            var index = val % SubBasketsCount;
            val /= SubBasketsCount;
            if (!Baskets.ContainsKey(index))
            {
                Baskets.Add(index, new LongHashSet());
            }
            Baskets[index].Add(val);
        }
    }

    public bool Contains(long val)
    {
        if (val < SubBasketsCount)
            return Baskets.ContainsKey(val) && Baskets[val].HaveNumber;
        else
        {
            var index = val % SubBasketsCount;
            return Baskets.ContainsKey(index) && Baskets[index].Contains(val/SubBasketsCount);
        }
    }

    public byte[] Serealize()
    {
        using (MemoryStream memoryStream = new MemoryStream())
        {
            Serializer.Serialize(memoryStream, this);
            return memoryStream.ToArray();
        }
    }
    
    public static LongHashSet DeserializeFromByteArray(byte[] data)
    {
        using (MemoryStream memoryStream = new MemoryStream(data))
        {
            return Serializer.Deserialize<LongHashSet>(memoryStream);
        }
    }
}

// [FirestoreData]
// public class LongHashSetDto
// {
//     [FirestoreProperty] public byte[] data { get; set; }
// }
//
// public class UserIdsHashSet
// {
//     private readonly FirestoreRepository<LongHashSetDto> _repo;
//
//     private LongHashSet _baskets = new LongHashSet()
//     {
//         SubBasketsCount = 10
//     };
//
//     public UserIdsHashSet(FirestoreDb firestoreDb)
//     {
//         _repo = new FirestoreRepository<LongHashSetDto>(firestoreDb, "UserIdsHashSetCollection");
//     }
//
//     public async Task Save()
//     { 
//         foreach (var basket in _baskets.Baskets)
//         {
//             var dto = new LongHashSetDto()
//             {
//                 data = basket.Value.Serealize()
//             };
//             await _repo.UpdateAsync(basket.Key.ToString(), dto);
//         }
//     }
//
//     public async Task Load()
//     {
//         if (_baskets.Baskets != null)
//             _baskets.Baskets.Clear();
//         _baskets.Baskets = new Dictionary<long, LongHashSet>();
//         var dtos = await _repo.GetAllAsync();
//         foreach (var dto in dtos)
//         {
//             var index = int.Parse(dto.Item1);
//             _baskets.Baskets.Add(index, LongHashSet.DeserializeFromByteArray(dto.Item2.data));
//         }
//     }
//
//     public void Add(long userId)
//     {
//         _baskets.Add(userId);
//     }
//
//     public bool Contains(long userId)
//     {
//         return _baskets.Contains(userId);
//     }
// }