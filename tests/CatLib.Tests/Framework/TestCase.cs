using System;
using System.Collections.Generic;

namespace CatLib.Tests.Framework;

public abstract class TestCase
{
    public abstract string Suite { get; }

    public virtual string Name => GetType().Name;

    public virtual int Order => 0;

    public virtual TimeSpan Timeout => TimeSpan.FromSeconds(15);

    public string FullName => Suite + "/" + Name;

    public abstract IEnumerable<TestStep> Run(TestContext context);
}
