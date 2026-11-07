using CampusGear.Services;
namespace CampusGear.Infrastructure;
public sealed class AccountFlowStore(IHttpContextAccessor accessor) : IAccountFlowStore
{
 private ISession Session => accessor.HttpContext?.Session ?? throw new InvalidOperationException("An account flow requires an HTTP session.");
 public string? GetString(string key)=>Session.GetString(key);
 public void SetString(string key,string value)=>Session.SetString(key,value);
 public void Remove(string key)=>Session.Remove(key);
 public void Clear()=>Session.Clear();
}
