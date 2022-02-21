using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Presentation.Models
{
    [FirestoreData]
    public class Message
    {
        [FirestoreProperty]
        public string Id { get; set; }
        [FirestoreProperty]
        public string Text { get; set; }
        [FirestoreProperty]
        public string Recipient { get; set; }
        
        [FirestoreProperty, ServerTimestamp]
        public Timestamp DateSent { get; set; }
    }
}
