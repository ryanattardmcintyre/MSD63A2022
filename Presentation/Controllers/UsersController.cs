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
        public async Task<IActionResult> Send(Message msg, IFormFile attachment )
        {
            msg.Id = Guid.NewGuid().ToString(); //unique id
            try
            {
                if (attachment != null)
                {
                    //1. will upload attachment to cloud storage 

                    var storage = StorageClient.Create();
                    using (Stream myUploadingFile = attachment.OpenReadStream())
                    {
                        storage.UploadObject(bucketName, msg.Id + System.IO.Path.GetExtension(attachment.FileName), null, myUploadingFile);
                    }

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
