using Nalix.Common.Attributes;
using Nalix.Common.Enums;
using System.Collections.Generic;

namespace Nalix.Shared.Tests.Cryptography;

public sealed class SimpleSensitiveModel
{
    [SensitiveData(DataSensitivityLevel.Confidential)]
    public System.String SecretString { get; set; }

    [SensitiveData(DataSensitivityLevel.Confidential)]
    public System.Int32 SecretNumber { get; set; }

    // Below threshold -> should be ignored by encryptor
    [SensitiveData(DataSensitivityLevel.Internal)]
    public System.String NonSensitive { get; set; }
}

public sealed class NestedChildModel
{
    [SensitiveData(DataSensitivityLevel.Confidential)]
    public System.String ChildSecret { get; set; }
}

public sealed class ComplexSensitiveModel
{
    [SensitiveData(DataSensitivityLevel.Confidential)]
    public System.String RootSecret { get; set; }

    [SensitiveData(DataSensitivityLevel.Confidential)]
    public List<NestedChildModel> Children { get; set; }

    [SensitiveData(DataSensitivityLevel.Confidential)]
    public NestedChildModel[] ChildArray { get; set; }

    [SensitiveData(DataSensitivityLevel.Confidential)]
    public NestedChildModel SingleChild { get; set; }
}