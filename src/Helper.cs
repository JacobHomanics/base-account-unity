// ===== HELPER CLASSES =====

/// <summary>
/// Helper class for JSON array deserialization.
/// Unity's JsonUtility doesn't support direct array deserialization,
/// so this wrapper provides a workaround for parsing JSON arrays.
/// </summary>
public static class JsonHelper
{
    /// <summary>
    /// Deserializes a JSON array into a typed array.
    /// </summary>
    /// <typeparam name="T">Type of array elements</typeparam>
    /// <param name="json">JSON array string</param>
    /// <returns>Array of deserialized objects</returns>
    public static T[] FromJson<T>(string json)
    {
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>("{\"Items\":" + json + "}");
        return wrapper.Items;
    }

    /// <summary>
    /// Internal wrapper class for JSON array deserialization
    /// </summary>
    /// <typeparam name="T">Type of array elements</typeparam>
    [Serializable]
    private class Wrapper<T>
    {
        public T[] Items;
    }
}
