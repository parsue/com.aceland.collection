#if ACELAND_LICENSING
using System;
using AceLand.Licensing;
using AceLand.Collection.Editor.Licensing;
using UnityEditor;
using UnityEngine;

namespace AceLand.Collection.LicenseBridge
{
    /// <summary>
    /// 授權橋接(source-only,由 Unity 以 versionDefines 條件編譯)。
    ///
    /// 僅當使用者安裝 <c>com.aceland.licensing</c> 時,Unity 才會定義 <c>ACELAND_LICENSING</c>
    /// 並編譯本檔。橋接於 <c>[InitializeOnLoad]</c> 向共用 Licensing Core 註冊產品描述子,
    /// 取得 <see cref="LicenseHandle"/>,再將包裝為 <see cref="ILicenseGate"/> 的
    /// <see cref="RealLicenseGate"/> 注入工具混淆 DLL 的 <see cref="LicenseGate"/> 接縫。
    ///
    /// 未安裝 Licensing 時本檔不存在於編譯,工具 DLL 的 <see cref="LicenseGate.Current"/>
    /// 維持 fail-closed 的 MissingLicenseGate,工具停用但不拋錯。
    /// </summary>
    [InitializeOnLoad]
    internal static class CollectionLicenseBridge
    {
        static CollectionLicenseBridge()
        {
            try
            {
                var descriptor = new LicenseProductDescriptor(
                    productId: "aceland_collection",
                    displayName: "AceLand Collection",
                    subscribeUrl: "https://subscription.parsue.io/l/collection",
                    trialDays: 7,
                    defaultMaxSeats: 1,
                    offlineGraceDays: 0,
                    storageFolder: null,
                    purchaseOnceUrl: "https://subscription.parsue.io/l/collection_permanent");

                var handle = LicenseService.Register(descriptor);
                LicenseGate.Install(new RealLicenseGate(handle));
            }
            catch (Exception e)
            {
                // 橋接失敗不得阻斷 Editor;維持 fail-closed 預設 gate。
                Debug.LogWarning($"[AceLand.Collection] License bridge init failed: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 將共用 Licensing Core 的 <see cref="LicenseHandle"/> 適配為工具 DLL 的
    /// <see cref="ILicenseGate"/>。所有授權判定 / UI 皆委派給 Core,工具 DLL 因此
    /// 完全不需硬參考 <c>AceLand.Licensing.*</c>。
    /// </summary>
    internal sealed class RealLicenseGate : ILicenseGate
    {
        private readonly LicenseHandle _handle;

        public RealLicenseGate(LicenseHandle handle) =>
            _handle = handle ?? throw new ArgumentNullException(nameof(handle));

        public bool IsEntitled => _handle.IsEntitled;

        public event Action Changed
        {
            add => _handle.Changed += value;
            remove => _handle.Changed -= value;
        }

        public void EnsureChecked() => _handle.EnsureChecked();

        public bool EnsureEntitledForMenu(string toolName) =>
            LicenseGuidance.EnsureEntitledForMenu(_handle, toolName);

        public void DrawBlockedPage(string toolName) =>
            LicenseGuidance.DrawBlockedPage(_handle, toolName);

        public void OpenLicenseWindow() => LicenseWindow.Open(_handle);
    }
}
#endif
