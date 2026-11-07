namespace CampusGear.Services;
// Session transport is implemented in WebApp. Missing (null) and unknown-account ("") markers are distinct.
public interface IAccountFlowStore
{
 string? GetString(string key);
 void SetString(string key,string value);
 void Remove(string key);
 void Clear();
}
