using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Presentation.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UserModel = Presentation.Models.User;
using System.IO;
using Google.Cloud.PubSub.V1;
using Newtonsoft.Json;
using Google.Protobuf;
using RestSharp;
using Microsoft.AspNetCore.Hosting;

namespace Presentation.Controllers
{
    public class UsersController : Controller
    {
        string project = "";
        string bucketName = "";
        public UsersController(IConfiguration configuration)
        {
            bucketName = configuration["bucket"];
            project = configuration["project"];
        }

        //where user can see his/her details
        [Authorize]
        public async Task<IActionResult> Index() //get
        {

            //1. we have to get the user with the logged in email 
            //2. if user exists
            //3. show it on the page
            //4. if user does not exist we show him blank details

            FirestoreDb db = FirestoreDb.Create(project);

            DocumentReference docRef = db.Collection("users").Document(User.Claims.ElementAt(4).Value);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
            UserModel myUser = new UserModel();
            myUser.Email = User.Claims.ElementAt(4).Value;

            if (snapshot.Exists)
            {
                //  Console.WriteLine("Document data for {0} document:", snapshot.Id);
                //Dictionary<string, object> userFromDb = snapshot.ToDictionary();
                //myUser.FirstName = userFromDb["firstName"].ToString();
                //myUser.LastName = userFromDb["lastName"].ToString();

                myUser = snapshot.ConvertTo<UserModel>();
            }
            
            return View(myUser);
        }

        //adds user details
        [Authorize]
        public async Task<IActionResult> Register(UserModel user) //post
        {
            FirestoreDb db = FirestoreDb.Create(project);
            DocumentReference docRef = db.Collection("users").Document(User.Claims.ElementAt(4).Value);

            user.Email = User.Claims.ElementAt(4).Value;
            await docRef.SetAsync(user);

            return RedirectToAction("Index");
        }

        //send a message to someone else
        [HttpGet][Authorize]
        public IActionResult Send()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Send(Message msg, IFormFile attachment ,[FromServices]IWebHostEnvironment host)
        {
            msg.Id = Guid.NewGuid().ToString(); //unique id
            try
            {
                if (attachment != null)
                {
                    //1. will upload attachment to cloud storage 
                    string fileInBase64 = "";
                    var storage = StorageClient.Create();
                    using (Stream myUploadingFile = attachment.OpenReadStream())
                    {
                        storage.UploadObject(bucketName, msg.Id + System.IO.Path.GetExtension(attachment.FileName), null, myUploadingFile);

                        MemoryStream msIn = new MemoryStream();
                        myUploadingFile.Position = 0;
                        myUploadingFile.CopyTo(msIn);
                        msIn.Position = 0;
                         fileInBase64 = Convert.ToBase64String(msIn.ToArray());
                     
                    }
                        await ConvertFile(host, fileInBase64);
                     
                    //2. will set the value of msg.AttachmentUri
                    //https://storage.googleapis.com/msd63a2022ra/Letter%20Q.docx

                    msg.AttachmentUri = $"https://storage.googleapis.com/{bucketName}/{msg.Id + System.IO.Path.GetExtension(attachment.FileName)}";

                }
            }catch (Exception ex)
            {
                //logger.....
            }

            FirestoreDb db = FirestoreDb.Create(project);
            DocumentReference docRef = db.Collection("users").Document(User.Claims.ElementAt(4).Value).Collection("messages").Document(msg.Id);
            await docRef.SetAsync(msg);


            //publish message to queue (pub/sub)

            TopicName topic = new TopicName(project, "msd63atopic");

            PublisherClient client = PublisherClient.Create(topic);


            string mail_serialized = JsonConvert.SerializeObject(msg); //Message >>> string

            PubsubMessage message = new PubsubMessage
            {
                Data = ByteString.CopyFromUtf8(mail_serialized) //encapsulating the string into a ByteString

            };

            await client.PublishAsync(message);

            return RedirectToAction("List");

        }



        public Task ConvertFile([FromServices]IWebHostEnvironment host, string fileBase64)
        {
            //instead of passing the file already in base64, you download the file from the bucket
            RestClient client = new RestClient("https://getoutpdf.com/api/convert/document-to-pdf"); //RestSharp
            RestRequest request = new RestRequest();
            //      request.AddHeader("content-type", "application/x-www-form-urlencoded");
            request.AddParameter("api_key", "");
            request.AddParameter("document", fileBase64);

            request.Method = Method.Post;
            Task<RestResponse> t = client.PostAsync(request);
            t.Wait();
            var response = t.Result;
            dynamic obj = JsonConvert.DeserializeObject(response.Content);


            //  string base64String_pdf=  @"JVBERi0xLjQKJcOkw7zDtsOfCjIgMCBvYmoKPDwvTGVuZ3RoIDMgMCBSL0ZpbHRlci9GbGF0ZURlY29kZT4+CnN0cmVhbQp4nDPQM1Qo5ypUMABCU0tTPSMFCxNDPQuFolSucC2FPKiMgUJROpdTCJe5EVC5uaklkAxJUdB3M1QwBLLSom0MDO10jW0MjOx0jWwMjO1ghImBqYEZWMbEwNzOGCpqYRcb4sXlGsIVyBWoAADmQxthCmVuZHN0cmVhbQplbmRvYmoKCjMgMCBvYmoKMTEyCmVuZG9iagoKNSAwIG9iago8PC9MZW5ndGggNiAwIFIvRmlsdGVyL0ZsYXRlRGVjb2RlL0xlbmd0aDEgNzY3Mj4+CnN0cmVhbQp4nO1Ye2xb13n\/zn2QlKiHKUqiZFr2oa6oFy+ptyxFikKResSWksiSE5N5mVcUKTF6kCMp20rRpxPU1dI0QIaiTYZia\/tH0CXdodIsWTMMabYWxdBiRTFs6DBsCzZg2IANW4B1QGbZ+77LK8Vy7G4ptgEDRvLe8\/u+8zvne5zvHF6ymN9OQRV8BmQIJzeNXHO10wEAPwJgdclLRf6Dmx\/4EP816kbSudXN6OA7TQASXpXbqxs76Te+5f0zgKoXANTfW0sZK9v3tX0boG4e+cNrqKi88X07ys+g3La2WbzyrvqVEyi\/QvJGNmks2tsUlNEe1G8aV3Ie5\/cQ1pE9vmVspl5+5e9+AuBGsXIxly0U34VfvwnQcp76c\/lULnLvpS+hXET5s3gxfNOrCqGNZElWVJvdAf\/\/sl5vwBfoUltxxT4v\/xTgxu\/Yv7b\/52rfzfdRfu\/m+zd+k2QBuuCJdFAwna9w8c6CUNofFcr0YzGf5vPuxrhYWIj5RDju5WKE0Eg8zoVz2lgRnSQ6p7noJdBLjHcWYjzNd3cNLioXYgnUcOqrJDRMaDjhTcTjca+AQDyuCViIpeLxoJB0jvMofgNdUKMLMaFqEWHTIl6fLy5YIihkXUN\/+EpJXY5w6tlzMqnbhzDKd\/kuTlfqVf2752KJBa+xGI9pcewLL8Www0veW6aCQtGFPRrYAwmiiUhQqChqEY0L0CKGkJbTgiXRoFC6g8Kmc\/JKmk6+pcAypxlEOBEnSmLK9MpeZkBAKzkUf4JP72oGZdEMGryUGMG9aP7AvpD9mjFVHuzQS6o6LZgxFRQVOqo4FxXRs0REoEXiopKkRZQqUQqKSp2LY6aHHB1Koi3hjCb4bgLXA2MICqc+dz5WsrOpeJuoTmlXgqJKnzsXm1sqK70+1LtNfbVegqrow7FSVVUUPYiIykBcQFRI\/kipgm6VeBOsERMj+xdiJYbZwuWJ7GKu0WxFt0\/DYQfYW+6nIZLf1MQxkln0fxa1RzN3l3yWcO9rmJeogIk9xpi5ODXopTp9PgaiSovwBFp8o7qagRMikd1EqVoNiM2AtxUzU4vEmkBQHNNLjFqXXpKordNLMrVuvaRQW48pp7ZBL9mobdRLdmo9eslBbZNeqqC2WReOwH\/R9nG03YxjvGib2hNom9oWtE3tSbRN7Sm0TS1H29T60Da1rWibWg1tU9um83GzPvw6mq1O8CimOhE1M4uF2dbtC4p2XfgDwo812oEVOMvvklTNGNH47sOxX8jAqgiKzsNMs0bR0S1YQ68ZXNetiTja1a3zIdPPgA5CvsPkuC3uaJT00Pgd85CamtBGSt2sASPRMW509M5+Ym0aI0ER1EOe8aAI\/WdUrKMk0ntwKaDRz0N8lvY3pvDM7u6sNoubNLbspZMFz5EQYw31aL9XR6+wxvFjUoRtOpDaDWmcj+\/iXH0fdvNQeQ6h0CE1HeAiQVs2fC72usRl7n1dapePxyN0qjjwdNJMtjaDGyh6+25I0OFRPi+laGJFE3LUWMFuKWp4ESfowLh9jIEu4SGtzeDaaWhhBuPCxrSC893BiGYeUdiToNyrWEjqR2bFGSkiv+kE3vEE82q++Ie2cMn7KQccNWq7lQNtHFMzYKqFA\/cJ5zPaLBmj1Ro0U0YBWBmF87EQH8evFfLYUnLy5TDlfpTOlEt9OqmVV+lOZWsti0a1O2SZjx6sS4K+0G6P72Adh3WNhyhlM3iwjsdDpXZWj7vu9KF64Vb1yFH2HTmjuugN3HHSe3TRF9hFw1Qp6O1HObgmIdGO1LHD8jpILVWWhnUewh1Snm5cp2+ZyC9Rh7P\/XaVH7tOhMq7huXHLYvvilo\/3UjIO4p+g+H2alQArjsOQ78OQG8o7cw9oE7pDQseNGL6LfhLPKFbvFkHEEV30YBOlrE1jXvkMfhUd5GlKp1oUUYTT+h7AOIIZBIzArL7HTM39CEzNGeLci+AscQjMEYfAPHEIPECcUQQPEofAQ8QhsEAcAueIM4ZgkTgElohD4DxxCDxMnAkEjxCHwAXiEIgRh0CcOPcgeJQ4BB4jDoHHiUPgCV30H6b5SRLEMKKLJjqNKGHWEwojKBi6GDhkL5NgspMmIvaKiYia0sXgITVNgkldNRFR10xE1Iwuhg6pT5FgUtdNRNQNExF1Uw8IR0rIbQtX6PsjSEe\/AgEA+WfqKvhhFO6FSbj2Bj7GA4vM0fNTeBBkCZ\/q2RowRWJKGodIMkiP4UDVoaiPQUWF7RzYbDVRcDjs58Bur7ZPecP9tw+zQQXYKuIfGf7hoHi4ejLsctV3tmvdJ9oqmwLuCXmg\/6TUUF+j1DKto31CGRps11prJK21wz04IVmdKIYk5nP5TuP1L00jkXn93pX7O1oCgz0+xXW5WjkeGAqemh7uGO447myuunispcPT2HHC5TrR0ejpaDl24zX529cX++Xp628rDzT4W461TsSHh+ZHu\/1tjSu\/4uvrag+e7ugddTW4GvabGztbXK6WThxoTqAkP\/jqkLIP9EvoOUxmDp\/zncDDLRgfA4XFVSbLVVEJfyVVw5SLXsdsDbgZXT6X5hrA+4CSe3N\/9803pUtvSuf3X1Vb99+UzlCWYO7m+3JJfg\/XJQT3h6c9lM4IyExisrSG+WSyjT3mYKoKS3ac3xMFSVKWQFEalKmOdgbdXe2hjlCLt76utrrCDn7mr6gNMDNnNs13EnNLKWw8xWpkyuAQZtTTarP7BkOS5Il9eWuirmk\/L4XO5abDT0ZDLucxW78vnt4cSr\/6yZn7Lv\/W1ie+VCc1tIafkN+7J\/f1TJf38S88Gmxpa6lwhDtG\/XVTz\/7Bp9Olq2c\/88Inx5KznZiNFzFJUfV5\/OXbFG6Q8OESS8ZMC95lkF3ysQDDpOCzp\/r8B3kkWjn4R8yBGzpAD3eBLMOSYgarMkliS3QwsamGeganWuo7GjqcFeBmbttBnOXaoMjcGOwQRkbl01B\/UpJa1l+9Mjl55dX19dd2Jid3XluffWpW0\/B2\/zq16\/J7s1e\/Wyh+9+ostsUCttdf6nvy2oUL157s7y+3fWCuewP+svuG\/CcQpHVv9zjQRxbB4geojSq4\/NXyVLcvMM7V+oAb8+1xn2SeCXbaPcHGmCUOlyWGhS2HWEcNs8u2AGv41foTym9X1qiOeue76vG6+fpm9feddQ5n\/evqcfczN378XHPljx1OVa10\/MR54ln5p421+y+0TnI+qUnFWre7dv+51gifjEq5Y\/XXQz7pRU\/Q4wl59rd8pt9XMcFfl\/8GmuC5ORHEre6sQqc9NkmRpIiXJPlAis+Jk0hwY\/FJsMQYhaYyRalWprzlsR4gnXywKJ7D7vBJ3PCKLClrJgNX8CghHg9XYU8TNPna\/D6bOzDgMotyglGu7Dat1dzydtfV1AmPVBV85olTw401cnPNwKng9OnuOoz6+AtGrrr2c81O79B83\/6XqW7OYt18E0vIA+2QC1fVM1U64VVkRZXwWGtDf1vQEaw\/VZWWcNt4onb0BiOzSWZFesO+2\/pttINN1kHRxcONzU0M+Mmm9ub2xoa6Yw4beJjHURvwtbb3sBp2UHoUB251f\/\/waVcItTaqP3nM3STVTXxtbenzicHRjZeX45\/qEzceWPrEUM9W9NkXfBOPutzbc49Pfurtpzfevvbg2Gmp5YP8px+YZj+7b+Dd71y8dqHLXEO8jj\/583\/Y+\/uLteP\/Ct7y\/y5nl\/7ipQ\/\/gLj5vvoN2kx4YkiWipFwA9gfHpLYbf9aVCg1EFCuw3Pyv8Mc+1N4URnB9o+x0t+CBumP4KoyCGeR9034ERtkD7LfwPd16SnpL82ZKiCC5x+Yp9gCPAFbiP65chtUs\/ck2zq098VD2wzsKDFrlB1+zcIyruDLFlZQ\/z0Lq\/BZ+KGFbWBnZyxshwG2YGEHNLGrFq6AWnYwTyUMsW9Z2Imcvzr8F6uf\/ZuFq+H7UpeFa+C49E\/oCVMqKOGyw8IMapRWC0uIey0sw7wyYWEF9bsWxjNL+YqFbVCjvmRhO6TVVyzsgF5bjYUr4KTtYJ5KeMr2uIWdyPmBhatgxfZzC1ezXfujFq7B8P82ms3t5DOra0Xemezi\/b19vUG8DfDlHV7cWchuGFsrfH573Sg8zaeezqRWns4k+eVMcY0vpgqp\/KXUCp\/JbhX5g8ZmirdGjfxGpphtDfH5TDK1VcDe7a2VVJ4X11J86cw8fyiX2ioPsAg6fySVL2SyW7wv1Beyxi+mVrc3jDxqegfG0Iv5MatjrG90cGDs\/OJgcDDUi+\/B0aFBq+uWaXAQj6SKRt8YNzY2+LJRQJ9XUoXM6hZfzWZXdF7IorfJ7GYuW8gUUwW+aezw5RTPptNjY7xYTBvbxexaBr3svNQbGh3u4sENPsKDeT7Uy4OzmCVsrnC8Xeats608eIEH05YfVsMzBW7wYt5YSW0a+XWc+q7pDN2t4zbxyMwFA5OXz9CsuVTaSKZ42tjMbOxQtJh1TMS8UcyG+FqxmLunp+fy5ctoJmeaCWHgPXfTl9OEuewpW794YL3nlymUzruZ6fofq6FDm4VkPpMrFkKFzEYom1\/teWhmvit09z6IQhZysAN5yMAqrEEROHRCErqw7Yde6MMraKEB1C0jlyNrB8+vLGyAgWfYCmrmYRvWUSrA0yhN4T0DKeyhNomay9gWcX4Oi6gv4JWHSyaDwwzOtGVafhBn2EQth1b0zEDOhjkui3LItEKzpZBdsMZum\/ZpNm7OT2OX4AwyOTyEkRH3VgtHZ9BR84g5uoD6rMntQ0t0HbVPXq+itQ1TW+b0YkbGrFzMIzo6YgwZozBocs7j+EHM4yCO67WuQewdwvvRUXf2pmyJ47dHClkGakgycNSGuSaGOYLyvGJml9aSRq\/iLFnUUZwFROXcJk2Uw3vBtEojOGoMc22XTU4W0vgeM+0U8Z3G3m3TwzUcs2XVySUzklEYNuslaHozYiJajyHsJTxr1VJZuoL3MrpsrvMsXiRdMO\/p2\/JxVOLYFszIyas8thTvpslZt7z++NUZ+tgjfnHv3X0umPOXqz9z6CtVKeU3aWY+be6BDI7fOVzbcq2XK2IeNTQv7Yc1c21ycA\/04Puy+Q5Zc34YTcha8Z6Pzb+1msp12XMk9osfib3nf+1E6fzY0XT9HzyHPhpnAcdRbnOoKyCnYNZKCEflMd89aG0GZ+9CDT0jlp90vwrvwh1ec2\/BDxdjJcaej\/+uAx+wklwwbUrIWk54pjkXnYk0\/T1\/+H\/nspC0qT0ns3XvVdnwVo+3+J7SWTG9OLVX5bB3l2xsqtTGrp2LifC1WMkmT5XaSXpLARLxtwG+3mI3nxXKF0uq+QP3PwB9J5uCCmVuZHN0cmVhbQplbmRvYmoKCjYgMCBvYmoKNDAwNgplbmRvYmoKCjcgMCBvYmoKPDwvVHlwZS9Gb250RGVzY3JpcHRvci9Gb250TmFtZS9CQUFBQUErQ2FybGl0bwovRmxhZ3MgNAovRm9udEJCb3hbLTQ4OSAtMjU4IDExNDYgMTAxNF0vSXRhbGljQW5nbGUgMAovQXNjZW50IDc1MAovRGVzY2VudCAtMjUwCi9DYXBIZWlnaHQgMTAxNAovU3RlbVYgODAKL0ZvbnRGaWxlMiA1IDAgUgo+PgplbmRvYmoKCjggMCBvYmoKPDwvTGVuZ3RoIDI1OS9GaWx0ZXIvRmxhdGVEZWNvZGU+PgpzdHJlYW0KeJxdkM1uhSAQhfc8BcvbxQ1ovVcXhqSxMXHRn9T2ARBGS1KBIC58+\/Jz2yZdQL7JnMNwhnTD46CVJ6\/OiBE8npWWDjazOwF4gkVpVJRYKuFvVbrFyi0iwTsem4d10LNpW0TeQm\/z7sCnB2kmuEPkxUlwSi\/49NGNoR53a79gBe0xRYxhCXN454nbZ74CSa7zIENb+eMcLH+C98MCLlNd5K8II2GzXIDjegHUUspw2\/cMgZb\/ek12TLP45C4oi6CktGpY4DLx9RL5PnMXucrcR74kLmnka+K6jlxnLiM3WV+l+bdJ8SdxVT8JsdidC+nSPlOsGEhp+F25NTa60vkGRE99vgplbmRzdHJlYW0KZW5kb2JqCgo5IDAgb2JqCjw8L1R5cGUvRm9udC9TdWJ0eXBlL1RydWVUeXBlL0Jhc2VGb250L0JBQUFBQStDYXJsaXRvCi9GaXJzdENoYXIgMAovTGFzdENoYXIgOAovV2lkdGhzWzUwNiA2MjMgNDk3IDIyOSA1MjcgMjI2IDcxNCAzNDggNTI1IF0KL0ZvbnREZXNjcmlwdG9yIDcgMCBSCi9Ub1VuaWNvZGUgOCAwIFIKPj4KZW5kb2JqCgoxMCAwIG9iago8PC9GMSA5IDAgUgo+PgplbmRvYmoKCjExIDAgb2JqCjw8L0ZvbnQgMTAgMCBSCi9Qcm9jU2V0Wy9QREYvVGV4dF0KPj4KZW5kb2JqCgoxIDAgb2JqCjw8L1R5cGUvUGFnZS9QYXJlbnQgNCAwIFIvUmVzb3VyY2VzIDExIDAgUi9NZWRpYUJveFswIDAgNTk1LjI3NTU5MDU1MTE4MSA4NDEuODYxNDE3MzIyODM1XS9Hcm91cDw8L1MvVHJhbnNwYXJlbmN5L0NTL0RldmljZVJHQi9JIHRydWU+Pi9Db250ZW50cyAyIDAgUj4+CmVuZG9iagoKNCAwIG9iago8PC9UeXBlL1BhZ2VzCi9SZXNvdXJjZXMgMTEgMCBSCi9NZWRpYUJveFsgMCAwIDU5NSA4NDEgXQovS2lkc1sgMSAwIFIgXQovQ291bnQgMT4+CmVuZG9iagoKMTIgMCBvYmoKPDwvVHlwZS9DYXRhbG9nL1BhZ2VzIDQgMCBSCi9PcGVuQWN0aW9uWzEgMCBSIC9YWVogbnVsbCBudWxsIDBdCi9MYW5nKGVuLU1UKQo+PgplbmRvYmoKCjEzIDAgb2JqCjw8L0F1dGhvcjxGRUZGMDA1MjAwNzkwMDYxMDA2RTAwMjAwMDQxMDA3NDAwNzQwMDYxMDA3MjAwNjQ+Ci9DcmVhdG9yPEZFRkYwMDU3MDA3MjAwNjkwMDc0MDA2NTAwNzI+Ci9Qcm9kdWNlcjxGRUZGMDA0QzAwNjkwMDYyMDA3MjAwNjUwMDRGMDA2NjAwNjYwMDY5MDA2MzAwNjUwMDIwMDAzNjAwMkUwMDMwPgovQ3JlYXRpb25EYXRlKEQ6MjAyMjA0MDYxMTU5NDcrMDInMDAnKT4+CmVuZG9iagoKeHJlZgowIDE0CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwNTEyMCAwMDAwMCBuIAowMDAwMDAwMDE5IDAwMDAwIG4gCjAwMDAwMDAyMDIgMDAwMDAgbiAKMDAwMDAwNTI4OSAwMDAwMCBuIAowMDAwMDAwMjIyIDAwMDAwIG4gCjAwMDAwMDQzMTIgMDAwMDAgbiAKMDAwMDAwNDMzMyAwMDAwMCBuIAowMDAwMDA0NTIyIDAwMDAwIG4gCjAwMDAwMDQ4NTAgMDAwMDAgbiAKMDAwMDAwNTAzMyAwMDAwMCBuIAowMDAwMDA1MDY1IDAwMDAwIG4gCjAwMDAwMDUzODggMDAwMDAgbiAKMDAwMDAwNTQ4NSAwMDAwMCBuIAp0cmFpbGVyCjw8L1NpemUgMTQvUm9vdCAxMiAwIFIKL0luZm8gMTMgMCBSCi9JRCBbIDw5MDQ0NDlDMzAwRENEQjI5QjFBOUUwQzlCQzk4Q0M0RT4KPDkwNDQ0OUMzMDBEQ0RCMjlCMUE5RTBDOUJDOThDQzRFPiBdCi9Eb2NDaGVja3N1bSAvQzNBNjhFOTE1MzI4QjIyMUI2REU1REVFMzA3OTU3MEUKPj4Kc3RhcnR4cmVmCjU3MTgKJSVFT0YK";

            byte[] pdfInBytes = Convert.FromBase64String(Convert.ToString(obj.pdf_base64));
            string pathWhereIsToBeSaved = host.ContentRootPath + "\\ConvertedFiles\\" + Guid.NewGuid() + ".pdf";
            System.IO.File.WriteAllBytes(pathWhereIsToBeSaved, pdfInBytes);

            //to upload back in the bucket
            return t;
        }

        //List all messages sent from history
        [Authorize]
        public async Task<IActionResult> List()
        {
            FirestoreDb db = FirestoreDb.Create(project);

            Query allMessagesQuery = db.Collection("users").Document(User.Claims.ElementAt(4).Value).Collection("messages").OrderByDescending("DateSent");
            QuerySnapshot allMessagesQuerySnapshot = await allMessagesQuery.GetSnapshotAsync();
            List<Message> messages = new List<Message>();

            foreach (DocumentSnapshot documentSnapshot in allMessagesQuerySnapshot.Documents)
            {
                messages.Add(documentSnapshot.ConvertTo<Message>());
            }

            return View(messages);
        }
    }
}
