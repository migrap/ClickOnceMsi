using System;
using System.Deployment.Application;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Deployment.WindowsInstaller;

namespace SetupActions {
    public class CustomActions {
        static CustomActions() {
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            Debug();
        }

        [CustomAction]
        public static ActionResult CheckApplicationExists(Session session) {
            var deploymentManifest = new Uri(session["DeploymentManifest"]);
            var downloadTask = (Task<GetManifestCompletedEventArgs>)null;

            using(var host = new InPlaceHostingManager(deploymentManifest, false)) {
                downloadTask = host.DownloadManifestAsync();

                downloadTask.Wait();

                if(downloadTask.IsCanceled) {
                    return ActionResult.UserExit;
                }
                else if(downloadTask.IsFaulted) {
                    session.Message(InstallMessage.Error, new Record { FormatString = downloadTask.Exception.ToString() });
                    return ActionResult.Failure;
                }

                var manifest = downloadTask.Result;

                var result = manifest.ApplicationManifest.CheckApplicationExists();

                if(result == ActionResult.SkipRemainingActions) {
                    session.Log("{0} is already installed.", manifest.ProductName);
                }

                return result;
            }
        }

        [CustomAction]
        public static ActionResult DownloadApplication(Session session) {
            var deploymentManifest = new Uri(session["DeploymentManifest"]);
            var cancellationTokenSource = new CancellationTokenSource();
            var previousProgressPercentage = 0;
            var downloadTask = (Task)null;

            DisplayActionData(session, "Hello, World!");

            ResetProgress(session);

            using(var host = new InPlaceHostingManager(deploymentManifest, false)) {
                downloadTask = host.DownloadManifestAsync();

                downloadTask.Wait();

                if(downloadTask.IsCanceled) {
                    return ActionResult.UserExit;
                }
                else if(downloadTask.IsFaulted) {
                    session.Message(InstallMessage.Error, new Record { FormatString = downloadTask.Exception.ToString() });
                    return ActionResult.Failure;
                }
                // Requirements
                host.AssertApplicationRequirements(true);

                // Download
                downloadTask = host.DownloadApplicationAsync(progress => {
                    session.Log("Downloaded {0,10}", (((double)progress.ProgressPercentage) / 100d).ToString("P"));

                    IncrementProgress(session, progress.ProgressPercentage - previousProgressPercentage);

                    previousProgressPercentage = progress.ProgressPercentage;
                });

                downloadTask.Wait();

                if(downloadTask.IsCanceled) {
                    return ActionResult.UserExit;
                }
                else if(downloadTask.IsFaulted) {
                    session.Message(InstallMessage.Error, new Record { FormatString = downloadTask.Exception.ToString() });
                    return ActionResult.Failure;
                }
            }

            return ActionResult.Success;
        }

        [Conditional("DEBUG")]
        private static void Debug() {
            Debugger.Launch();
        }

        private static void ResetProgress(Session session, int value = 100) {
            var record = new Record(4);
            record[1] = 0;      // "Reset" message 
            record[2] = value - (value * 0.05); // total ticks;
            record[3] = 0;      // forward motion 
            record[4] = 0;
            session.Message(InstallMessage.Progress, record);

            record = new Record(4);
            record[1] = 1;
            record[2] = 1;
            record[3] = 0;
            session.Message(InstallMessage.Progress, record);
        }

        private static void IncrementProgress(Session session, int value) {
            var record = new Record(3);
            record[1] = 2;      // ProgressReport message 
            record[2] = value;  // ticks to increment 
            record[3] = 0;      // ignore 
            session.Message(InstallMessage.Progress, record);
        }

        private static void DisplayActionData(Session session, string message) {
            Record record = new Record(3);
            record[1] = "DownloadApplication";
            record[2] = "Hello WOrld";
            record[3] = "Hello record three";
            session.Message(InstallMessage.ActionStart, record);
        }
    }
}
