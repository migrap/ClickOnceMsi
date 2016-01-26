using System;
using System.Deployment.Application;
using System.IO;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Deployment.WindowsInstaller;

namespace SetupActions {
    public static class Extensions {
        internal static ActionResult CheckApplicationExists(this XmlReader reader) {
            if(reader.ReadToFollowing("description")) {
                var publisher = reader.GetAttribute("asmv2:publisher");
                var product = reader.GetAttribute("asmv2:product");
                var shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), publisher);

                shortcut = Path.Combine(shortcut, product) + ".appref-ms";

                if(File.Exists(shortcut)) {
                    return ActionResult.SkipRemainingActions;
                }
            }
            return ActionResult.Success;
        }

        public static Task<GetManifestCompletedEventArgs> DownloadManifestAsync(this InPlaceHostingManager manager) {
            var tcs = new TaskCompletionSource<GetManifestCompletedEventArgs>();

            manager.GetManifestCompleted += (sender, e) => {
                if(e.Error != null) {
                    tcs.SetException(e.Error);
                    return;
                }

                var trust = new ApplicationTrust();
                var permissions = new PermissionSet(PermissionState.Unrestricted);
                var statement = new PolicyStatement(permissions);

                trust.DefaultGrantSet = statement;
                trust.ApplicationIdentity = e.ApplicationIdentity;
                trust.IsApplicationTrustedToRun = true;

                ApplicationSecurityManager.UserApplicationTrusts.Add(trust);

                tcs.SetResult(e);
            };

            manager.GetManifestAsync();

            return tcs.Task;
        }

        public static Task<DownloadApplicationCompletedEventArgs> DownloadApplicationAsync(this InPlaceHostingManager manager, Action<DownloadProgressChangedEventArgs> callback = null) {
            var tcs = new TaskCompletionSource<DownloadApplicationCompletedEventArgs>();

            manager.DownloadProgressChanged += (sender, e) => {
                try {
                    callback(e);
                }
                catch(Exception) {
                    manager.CancelAsync();
                }
            };

            manager.DownloadApplicationCompleted += (sender, e) => {
                if(e.Error != null) {
                    tcs.SetException(e.Error);
                    return;
                }

                tcs.SetResult(e);
            };

            manager.DownloadApplicationAsync();

            return tcs.Task;
        }
    }
}
