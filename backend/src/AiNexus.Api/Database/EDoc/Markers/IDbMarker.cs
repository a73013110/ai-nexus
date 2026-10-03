using EDoc.Core.Database.Enums;

namespace EDoc.Core.Database.Markers;

/// <summary>
/// 資料庫定義基底介面
/// <para>所有資料庫定義都要實作此介面</para>
/// </summary>
public interface IDbMarker
{
    /// <summary>
    /// appsettings.json 中的連線字串 Key
    /// </summary>
    static abstract string ConnectionStringKey { get; }

    /// <summary>
    /// 資料庫 Provider 類型
    /// </summary>
    static abstract DbProviderType ProviderType { get; }
}
