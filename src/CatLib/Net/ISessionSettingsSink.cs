using System.Collections.Generic;

namespace CatLib.Net;

public interface ISessionSettingsSink
{
    SessionApplyResult Apply(IReadOnlyList<SessionSettingValue> values);

    int Clear();
}
