using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;

namespace TestBotMoinak
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            if (activity.Type == ActivityTypes.Message)
            {
                ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
                // calculate something for us to return
                //int length = (activity.Text ?? string.Empty).Length;
                string nextQuestion = ProcessTextMessage(activity.Text);
                // return our reply to the user
                Activity reply = activity.CreateReply($"{nextQuestion} {activity.From.Name}");
                await connector.Conversations.ReplyToActivityAsync(reply);
            }
            else
            {
                HandleSystemMessage(activity);
            }
            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        private Activity HandleSystemMessage(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData)
            {
                // Implement user deletion here
                // If we handle user deletion, return a real message
            }
            else if (message.Type == ActivityTypes.ConversationUpdate)
            {
                // Handle conversation state changes, like members being added and removed
                // Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
                // Not available in all channels
            }
            else if (message.Type == ActivityTypes.ContactRelationUpdate)
            {
                // Handle add/remove from contact lists
                // Activity.From + Activity.Action represent what happened
            }
            else if (message.Type == ActivityTypes.Typing)
            {
                // Handle knowing tha the user is typing
            }
            else if (message.Type == ActivityTypes.Ping)
            {
            }
            

            return null;
        }
        public string ProcessTextMessage(string text)
        {
            string locale = DetectTextLanguage(text);
            IntentWithEntities intentEntitesCapture = new IntentWithEntities();
            string nextQuestion = "";
            if (locale.Equals("en"))
            {
                Console.WriteLine("English text is currently supported.");
                intentEntitesCapture = MapIntent(text);

                if (intentEntitesCapture.intent == "Get Cheapest Ride")
                {
                    string source = GoogleLocationFormatter(intentEntitesCapture.rideEstimate.Source);
                    string destination = GoogleLocationFormatter(intentEntitesCapture.rideEstimate.Destination);
                    List<CabProvider> providerList = PopulateCabProvidersList("mumbai");
                    EstimatePrerequisite CalculatedDistanceDuration = GetDistanceDurationForJourney(source, destination);


                    List<ProviderWithEstimate> AllProvidersWithEstimatesList =
                        GetAllProvidersWithFareEstimates(CalculatedDistanceDuration, providerList, source, destination);

                    ProviderWithEstimate CheapestCabOption = GetCheapestCabOption(AllProvidersWithEstimatesList);

                    nextQuestion =
                        String.Format(
                            "It looks like you want to find out the cheapest way to go from {0} to {1}, and what I found is that {2} is the cheapest option and the fare estimate is INR  {3},",
                            intentEntitesCapture.rideEstimate.Source, intentEntitesCapture.rideEstimate.Destination,
                            CheapestCabOption.ProviderName, CheapestCabOption.FareEstimate);

                }
                else if (intentEntitesCapture.intent == "Track order")
                {
                    nextQuestion = "What is your tracking number, ";
                }
                
                else if (intentEntitesCapture.intent =="Greetings")
                {
                    nextQuestion = "Greetings to you too, ";
                }
                else
                {
                    return "Sorry, I don't understand that response. Supported operations are tracking an order";
                }
               return nextQuestion;
            }
            else
            {
                Console.WriteLine("Other languages not implemented yet");
            }
            return nextQuestion;
        }

        public string DetectTextLanguage(string text)
        {
            //NOT IMPLEMENTED YET
            return "en";
        }
        public string GetIntent(List<Intent> intents)
        {
            string intent = "";
            Intent probableIntent = new Intent();
            List<Intent> sortedListIntents = intents.OrderBy(o => o.score).ToList();
            intent = sortedListIntents.Last().intent;

            return intent;
        }

        public RideEstimateDetails GetRideShareDetailsForFareEstimate(List<Entity> entities)
        {
            RideEstimateDetails rideEstimate = new RideEstimateDetails();
            foreach (Entity e in entities)
            {
                if (e.type == "CabProvider")
                {
                    rideEstimate.CabProvider = e.entity;
                }
                else if (e.type == "Destination")
                {
                    rideEstimate.Destination = e.entity;
                }
                else if (e.type == "Source")
                {
                    rideEstimate.Source = e.entity;
                }
            }
            return rideEstimate;
        }
        public IntentWithEntities MapIntent(string resp)
        {
            IntentWithEntities IntentsAndActions = new IntentWithEntities();
            string requestURL = String.Format("https://api.projectoxford.ai/luis/v1/application?id={0}&subscription-key={1}&q={2}",
                   ConfigurationManager.AppSettings["ApplicationID"],
                  ConfigurationManager.AppSettings["SubscriptionKey"],
                   resp);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestURL);
            string intent = "";
            // Add the OAuth Authorization header, and Content Type header
            request.ContentType = "application/json";

            try
            {
                // Call the REST endpoint
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Console.WriteLine(response.StatusDescription);
                Stream receiveStream = response.GetResponseStream();

                // Pipes the stream to a higher level stream reader with the required encoding format. 
                StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8);
                var IntentStream = readStream.ReadToEnd();

                Console.WriteLine(IntentStream);

                // You can also walk through this object to manipulate the individuals member objects. 
                IntentPayload payload = JsonConvert.DeserializeObject<IntentPayload>(IntentStream);

                IntentsAndActions.intent = GetIntent(payload.intents);
                IntentsAndActions.rideEstimate = GetRideShareDetailsForFareEstimate(payload.entities);
                response.Close();
                readStream.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("{0} \n\n{1}", e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                Console.ReadLine();
            }

            return IntentsAndActions;
        }

        public List<CabProvider> PopulateCabProvidersList(string city)
        {
            List<CabProvider> providerList = new List<CabProvider>();

            string provider = "OlaLux";
            CabProvider OlaLux = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(OlaLux);

            provider = "OlaMini";
            CabProvider OlaMini = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(OlaMini);

            provider = "OlaPrimeSUV";
            CabProvider OlaPrimeSUV = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(OlaPrimeSUV);

            provider = "OlaPrimeSedan";
            CabProvider OlaPrimeSedan = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(OlaPrimeSedan);


            provider = "OlaMicro";
            CabProvider OlaMicro = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(OlaMicro);

            provider = "OlaEconomySedan";
            CabProvider OlaEconomySedan = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(OlaEconomySedan);



            provider = "MeruCabsDayTime";
            CabProvider MeruCabsDayTime = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(MeruCabsDayTime);



            provider = "MeruCabsNightTime";
            CabProvider MeruCabsNightTime = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(MeruCabsNightTime);

            provider = "TabCabDayFare";
            CabProvider TabCabDayFare = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(TabCabDayFare);

            provider = "TabCabNightFare";
            CabProvider TabCabNightFare = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(TabCabNightFare);

            provider = "TabCabGoldDayFare";
            CabProvider TabCabGoldDayFare = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(TabCabGoldDayFare);

            provider = "TabCabGoldNightFare";
            CabProvider TabCabGoldNightFare = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(TabCabGoldNightFare);


            provider = "EasyCabsDayTime";
            CabProvider EasyCabsDayTime = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(EasyCabsDayTime);

            provider = "EasyCabsNightTime";
            CabProvider EasyCabsNightTime = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(EasyCabsNightTime);


            provider = "MeruFlexi";
            CabProvider MeruFlexi = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(MeruFlexi);


            provider = "GenieDay";
            CabProvider GenieDay = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(GenieDay);

            provider = "GenieNight";
            CabProvider GenieNight = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(GenieNight);


            provider = "TaxiForSure_Hatchback";
            CabProvider TaxiForSure_Hatchback = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(TaxiForSure_Hatchback);

            provider = "TaxiForSure_Sedan";
            CabProvider TaxiForSure_Sedan = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(TaxiForSure_Sedan);

            //provider = "TaxiForSure_SUV";
            //CabProvider TaxiForSure_SUV = new CabProvider(
            //    provider,
            //    Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
            //    Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
            //    Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
            //    Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
            //    Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
            //    Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
            //    );
            //providerList.Add(TaxiForSure_SUV);

            provider = "KaliPeeliDayFare";
            CabProvider KaliPeeliDayFare = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(KaliPeeliDayFare);

            provider = "KaliPeeliNightFare";
            CabProvider KaliPeeliNightFare = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(KaliPeeliNightFare);

            provider = "AutoRickshawDayFare";
            CabProvider AutoRickshawDayFare = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(AutoRickshawDayFare);


            provider = "AutoRickshawNightFare";
            CabProvider AutoRickshawNightFare = new CabProvider(
                provider,
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Base", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerMinute", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-IncludedMin", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-PerKM", provider)]),
                Convert.ToDouble(ConfigurationManager.AppSettings[String.Format("{0}-Minimum", provider)])
                );
            providerList.Add(AutoRickshawNightFare);

            return providerList;
        }
        public EstimatePrerequisite GetDistanceDurationForJourney(string source, string destination)
        {
            EstimatePrerequisite distanceAndDuration = new EstimatePrerequisite();
            string requestURL = String.Format("{0}origins={1}&destinations={2}&key={3}",
           ConfigurationManager.AppSettings["GoogleDistanceUrl"],
           source,
           destination,
           ConfigurationManager.AppSettings["DistanceAPIkey"]);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestURL);

            try
            {
                // Call the REST endpoint
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream receiveStream = response.GetResponseStream();
                // Pipes the stream to a higher level stream reader with the required encoding format. 
                StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8);
                var usageResponse = readStream.ReadToEnd();
                DistanceMatrixPayload payload = JsonConvert.DeserializeObject<DistanceMatrixPayload>(usageResponse);

                distanceAndDuration.distance = Convert.ToDouble(payload.rows[0].elements[0].distance.value) / 1000;
                distanceAndDuration.duration = Convert.ToDouble(payload.rows[0].elements[0].duration.value) / 60;

                Console.WriteLine("Total Distance for trip:   " + distanceAndDuration.distance + " km");
                Console.WriteLine("Total Estimated Duration for trip:    " + distanceAndDuration.duration + " mins");
                Console.WriteLine();
                response.Close();
                readStream.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("{0} \n\n{1}", e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                Console.ReadLine();
            }
            return distanceAndDuration;
        }

        public List<ProviderWithEstimate> GetAllProvidersWithFareEstimates(EstimatePrerequisite DistanceDuration, List<CabProvider> providersList, string source, string destination)
        {
            List<ProviderWithEstimate> ProviderFaresList = new List<ProviderWithEstimate>();

            //Add fare estimates for all non rateCard cab companies (not Uber, currently keeping Ola in the list as I don't have a prod key for Ola)
            foreach (CabProvider provider in providersList)
            {
                ProviderWithEstimate CabEstimate = new ProviderWithEstimate();
                CabEstimate.ProviderName = provider.Name;
                CabEstimate.FareEstimate = provider.GetTotalRate(provider.Name, DistanceDuration.distance,
                    DistanceDuration.duration, provider.minfare);
                ProviderFaresList.Add(CabEstimate);
            }

            if (!(GetCoordinatesForLocation(source)[0] == 0.00 || GetCoordinatesForLocation(source)[1] == 0.00 || GetCoordinatesForLocation(destination)[0] == 0.00 || GetCoordinatesForLocation(destination)[1] == 0.00))
            {

                List<UberEstimate> AllUberEstimatesList = GetAllUberEstimates(GetCoordinatesForLocation(source)[0],
                 GetCoordinatesForLocation(source)[1], GetCoordinatesForLocation(destination)[0],
                 GetCoordinatesForLocation(destination)[1]);

                foreach (UberEstimate estimate in AllUberEstimatesList)
                {
                    ProviderWithEstimate CabEstimate = new ProviderWithEstimate();
                    CabEstimate.ProviderName = estimate.displayName;
                    CabEstimate.FareEstimate = estimate.midEstimate;
                    ProviderFaresList.Add(CabEstimate);
                }
            }

            return ProviderFaresList;
        }
        public ProviderWithEstimate GetCheapestCabOption(List<ProviderWithEstimate> ProvidersWithEstimatesList)
        {
            ProviderWithEstimate lowestCab = new ProviderWithEstimate();

            ProvidersWithEstimatesList = ProvidersWithEstimatesList.OrderBy(o => o.FareEstimate).ToList();
            lowestCab = ProvidersWithEstimatesList[0];

            foreach (ProviderWithEstimate estimate in ProvidersWithEstimatesList)
            {
                Console.WriteLine(String.Format("Cab name is {0} and fare estimate is INR  {1}", estimate.ProviderName, estimate.FareEstimate));
                Console.WriteLine();
            }
            Console.WriteLine();

            return lowestCab;
        }


        public string GoogleLocationFormatter(string input)
        {
            int len = input.Length;
            string result = "";

            for (int i = 0; i < len; i++)
            {
                if (input[i] == ' ')
                {
                    result = result + '+';
                }
                else
                {
                    result = result + input[i];
                }
            }
            return result;
        }
        public List<double> GetCoordinatesForLocation(string source)
        {
            string requestURLSource = String.Format("{0}address={1}&key={2}",
                      ConfigurationManager.AppSettings["GoogleGeoCodingUrl"],
                      source,
                      ConfigurationManager.AppSettings["GeocodingAPIkey"]);

            List<double> results = new List<double>();

            HttpWebRequest requestSource = (HttpWebRequest)WebRequest.Create(requestURLSource);

            try
            {
                // Call the REST endpoint
                HttpWebResponse SourceResponse = (HttpWebResponse)requestSource.GetResponse();
                Stream sourceStream = SourceResponse.GetResponseStream();
                // Pipes the stream to a higher level stream reader with the required encoding format. 
                StreamReader SourcereadStream = new StreamReader(sourceStream, Encoding.UTF8);
                var resp1 = SourcereadStream.ReadToEnd();
                GeocodingPayload RequestPayload = JsonConvert.DeserializeObject<GeocodingPayload>(resp1);

                if (RequestPayload.results.Count == 0)
                {
                    Console.WriteLine("Could not find coords for this location");
                    results.Add(0.00);
                    results.Add(0.00);
                }
                else
                {
                    results.Add(RequestPayload.results[0].geometry.location.lat);
                    results.Add(RequestPayload.results[0].geometry.location.lng);
                }
                Console.WriteLine();
                SourceResponse.Close();
                SourcereadStream.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("{0} \n\n{1}", e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                Console.ReadLine();
            }
            return results;
        }
        public List<UberEstimate> GetAllUberEstimates(double sourceLat, double sourceLong, double destinationLat, double destinationLong)
        {
            string requestURLSource = String.Format("{0}start_latitude={1}&start_longitude={2}&end_latitude={3}&end_longitude={4}&server_token={5}",
                      ConfigurationManager.AppSettings["UberPriceEstimateURL"],
                      sourceLat, sourceLong, destinationLat, destinationLong,
                      ConfigurationManager.AppSettings["UberServerToken"]);

            HttpWebRequest requestSource = (HttpWebRequest)WebRequest.Create(requestURLSource);


            List<UberEstimate> UberEstimatesList = new List<UberEstimate>();
            try
            {
                // Call the REST endpoint
                HttpWebResponse SourceResponse = (HttpWebResponse)requestSource.GetResponse();
                Stream sourceStream = SourceResponse.GetResponseStream();
                // Pipes the stream to a higher level stream reader with the required encoding format. 
                StreamReader SourcereadStream = new StreamReader(sourceStream, Encoding.UTF8);
                var resp1 = SourcereadStream.ReadToEnd();
                UberRatePayload RequestPayload = JsonConvert.DeserializeObject<UberRatePayload>(resp1);

                foreach (Price price in RequestPayload.prices)
                {
                    UberEstimate estimate = new UberEstimate();
                    estimate.lowEstimate = price.low_estimate;
                    estimate.highEstimate = price.high_estimate;
                    estimate.displayName = price.display_name;
                    estimate.distance = price.distance;
                    estimate.duration = price.duration;
                    estimate.surgeMultiplier = price.surge_multiplier;
                    estimate.midEstimate = (price.low_estimate + price.high_estimate) / 2;
                    UberEstimatesList.Add(estimate);
                }

                Console.WriteLine();
                SourceResponse.Close();
                SourcereadStream.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("{0} \n\n{1}", e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                Console.ReadLine();
            }

            return UberEstimatesList;
        }

        public List<UberTimeEstimate> GetAllUberTimeEstimates(double sourceLat, double sourceLong)
        {
            List<UberTimeEstimate> UberTimeEstimatesList = new List<UberTimeEstimate>();

            string requestURLSource = String.Format("{0}start_latitude={1}&start_longitude={2}&server_token={3}",
                      ConfigurationManager.AppSettings["UberTimeEstimateURL"],
                      sourceLat, sourceLong,
                      ConfigurationManager.AppSettings["UberServerToken"]);

            HttpWebRequest requestSource = (HttpWebRequest)WebRequest.Create(requestURLSource);

            try
            {
                // Call the REST endpoint
                HttpWebResponse SourceResponse = (HttpWebResponse)requestSource.GetResponse();
                Stream sourceStream = SourceResponse.GetResponseStream();
                // Pipes the stream to a higher level stream reader with the required encoding format. 
                StreamReader SourcereadStream = new StreamReader(sourceStream, Encoding.UTF8);
                var resp1 = SourcereadStream.ReadToEnd();
                UberTimeEstimatePayload RequestPayload = JsonConvert.DeserializeObject<UberTimeEstimatePayload>(resp1);

                foreach (UberTimeEstimate tempEstimate in RequestPayload.times)
                {
                    UberTimeEstimate estimate = new UberTimeEstimate();
                    estimate.estimate = tempEstimate.estimate / 60;
                    estimate.display_name = tempEstimate.display_name;
                    estimate.localized_display_name = tempEstimate.localized_display_name;
                    estimate.product_id = tempEstimate.product_id;
                    UberTimeEstimatesList.Add(estimate);
                    Console.WriteLine();
                    Console.WriteLine(String.Format("Cab name is {0} and time estimate is {1} mins", estimate.display_name, estimate.estimate));
                    Console.WriteLine();
                }

                Console.WriteLine();
                SourceResponse.Close();
                SourcereadStream.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("{0} \n\n{1}", e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                Console.ReadLine();
            }
            return UberTimeEstimatesList;

        }

        public UberEstimate GetCheapestUberEstimate(List<UberEstimate> UberEstimatesList)
        {
            UberEstimate cheapestUberEstimate = new UberEstimate();
            cheapestUberEstimate.lowEstimate = UberEstimatesList[0].lowEstimate;
            cheapestUberEstimate.highEstimate = UberEstimatesList[0].highEstimate;
            cheapestUberEstimate.displayName = UberEstimatesList[0].displayName;
            cheapestUberEstimate.distance = UberEstimatesList[0].distance;
            cheapestUberEstimate.duration = UberEstimatesList[0].duration;
            cheapestUberEstimate.surgeMultiplier = UberEstimatesList[0].surgeMultiplier;
            cheapestUberEstimate.midEstimate = (UberEstimatesList[0].lowEstimate + UberEstimatesList[0].highEstimate) / 2;

            double midEstimate = cheapestUberEstimate.midEstimate;

            foreach (UberEstimate estimate in UberEstimatesList)
            {
                if (midEstimate > estimate.midEstimate)
                {
                    cheapestUberEstimate.lowEstimate = estimate.lowEstimate;
                    cheapestUberEstimate.highEstimate = estimate.highEstimate;
                    cheapestUberEstimate.displayName = estimate.displayName;
                    cheapestUberEstimate.distance = estimate.distance;
                    cheapestUberEstimate.duration = estimate.duration;
                    cheapestUberEstimate.surgeMultiplier = estimate.surgeMultiplier;
                    cheapestUberEstimate.midEstimate = estimate.midEstimate;

                    midEstimate = cheapestUberEstimate.midEstimate;
                }
            }

            return cheapestUberEstimate;
        }

        public UberTimeEstimate GetLowestUberTimeEstimate(List<UberTimeEstimate> UberTimeEstimatesList)
        {
            UberTimeEstimate lowestUberTimeEstimate = new UberTimeEstimate();
            //lowestUberTimeEstimate.estimate = UberTimeEstimatesList[0].estimate;
            //lowestUberTimeEstimate.display_name = UberTimeEstimatesList[0].display_name;
            //lowestUberTimeEstimate.localized_display_name = UberTimeEstimatesList[0].localized_display_name;
            //lowestUberTimeEstimate.product_id = UberTimeEstimatesList[0].product_id;
            UberTimeEstimatesList = UberTimeEstimatesList.OrderBy(o => o.estimate).ToList();
            lowestUberTimeEstimate = UberTimeEstimatesList[0];
            return lowestUberTimeEstimate;
        }
        public UberEstimate GetCheapestUberEstimateFromPriceList(List<Price> UberPrices)
        {
            UberEstimate CheapestEstimate = new UberEstimate();
            CheapestEstimate.lowEstimate = UberPrices[0].low_estimate;
            CheapestEstimate.highEstimate = UberPrices[0].high_estimate;
            CheapestEstimate.displayName = UberPrices[0].display_name;
            CheapestEstimate.distance = UberPrices[0].distance;
            CheapestEstimate.duration = UberPrices[0].duration;
            CheapestEstimate.surgeMultiplier = UberPrices[0].surge_multiplier;
            CheapestEstimate.midEstimate = (UberPrices[0].low_estimate + UberPrices[0].high_estimate) / 2;

            double midEstimate = CheapestEstimate.midEstimate;

            foreach (Price price in UberPrices)
            {

                if (midEstimate > ((price.low_estimate + price.high_estimate) / 2))
                {
                    CheapestEstimate.lowEstimate = price.low_estimate;
                    CheapestEstimate.highEstimate = price.high_estimate;
                    CheapestEstimate.displayName = price.display_name;
                    CheapestEstimate.distance = price.distance;
                    CheapestEstimate.duration = price.duration;
                    CheapestEstimate.surgeMultiplier = price.surge_multiplier;
                    CheapestEstimate.midEstimate = (price.low_estimate + price.high_estimate) / 2;

                    midEstimate = (CheapestEstimate.lowEstimate + CheapestEstimate.highEstimate) / 2;
                }
            }
            return CheapestEstimate;
        }

        public OlaEstimate GetOlaEstimate(double sourceLat, double sourceLong, double destinationLat, double destinationLong)
        {
            string requestURLSource = String.Format("{0}pickup_lat={1}&pickup_lng={2}&drop_lat={3}&drop_lng={4}",
                      ConfigurationManager.AppSettings["OlaPriceEstimateURL"],
                      sourceLat, sourceLong, destinationLat, destinationLong
                     );
            OlaEstimate estimate = new OlaEstimate();
            HttpWebRequest requestSource = (HttpWebRequest)WebRequest.Create(requestURLSource);
            requestSource.Headers.Add("X-APP-TOKEN", ConfigurationManager.AppSettings["OlaServerToken"]);
            try
            {
                // Call the REST endpoint
                HttpWebResponse SourceResponse = (HttpWebResponse)requestSource.GetResponse();
                Stream sourceStream = SourceResponse.GetResponseStream();
                // Pipes the stream to a higher level stream reader with the required encoding format. 
                StreamReader SourcereadStream = new StreamReader(sourceStream, Encoding.UTF8);
                var resp1 = SourcereadStream.ReadToEnd();
                OlaRatePayload RequestPayload = JsonConvert.DeserializeObject<OlaRatePayload>(resp1);

                estimate.lowEstimate = RequestPayload.ride_estimate[0].amount_min;
                estimate.highEstimate = RequestPayload.ride_estimate[0].amount_max;
                estimate.displayName = RequestPayload.ride_estimate[0].category;
                estimate.distance = RequestPayload.ride_estimate[0].distance;
                estimate.duration = RequestPayload.ride_estimate[0].travel_time_in_minutes;
                estimate.midEstimate = (estimate.lowEstimate + estimate.highEstimate) / 2;

                double midEstimate = (estimate.lowEstimate + estimate.highEstimate) / 2;

                foreach (RideEstimate price in RequestPayload.ride_estimate)
                {

                    if (midEstimate > ((price.amount_min + price.amount_max) / 2))
                    {
                        estimate.lowEstimate = price.amount_min;
                        estimate.highEstimate = price.amount_max;
                        estimate.displayName = price.category;
                        estimate.distance = price.distance;
                        estimate.duration = price.travel_time_in_minutes;
                        estimate.midEstimate = (price.amount_min + price.amount_max) / 2;

                        midEstimate = (estimate.lowEstimate + estimate.highEstimate) / 2;
                    }
                }
                Console.WriteLine();
                SourceResponse.Close();
                SourcereadStream.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("{0} \n\n{1}", e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                Console.ReadLine();
            }
            return estimate;
        }
    }
    public class Intent
    {
        public string intent { get; set; }
        public double score { get; set; }
        public List<Action> actions { get; set; }
    }

    public class Action
    {
        public bool triggered { get; set; }
        public string name { get; set; }
        public List<Parameter> parameters { get; set; }
    }

    public class Parameter
    {
        public string name { get; set; }
        public bool required { get; set; }
        public List<Value> value { get; set; }
    }

    public class Value
    {
        public string entity { get; set; }
        public string type { get; set; }
        public double score { get; set; }
    }
    public class Entity
    {
        public string entity { get; set; }
        public string type { get; set; }
        public int startIndex { get; set; }
        public int endIndex { get; set; }
        public double score { get; set; }
    }

    public class IntentPayload
    {
        public string query { get; set; }
        public List<Intent> intents { get; set; }
        public List<Entity> entities { get; set; }
    }

    public class RideEstimateDetails
    {
        public string CabProvider { get; set;  }
        public string Source { get; set;  }
        public string Destination { get; set; }

    }

    public class IntentWithEntities
    {
        public string intent { get; set; }
        public RideEstimateDetails rideEstimate { get; set; }
    }

    public class EstimatePrerequisite
    {
        public double distance { get; set; }
        public double duration { get; set; }
    }

    public class ProviderWithEstimate
    {
        public string ProviderName { get; set; }
        public double FareEstimate { get; set; }

        public ProviderWithEstimate()
        {

        }
        public ProviderWithEstimate(string ProviderName, double FareEstimate)
        {
            this.ProviderName = ProviderName;
            this.FareEstimate = FareEstimate;
        }
    }

    public class OlaEstimate
    {
        public double lowEstimate { get; set; }

        public string displayName { get; set; }

        public double duration { get; set; }

        public double distance { get; set; }

        public double highEstimate { get; set; }

        public double midEstimate { get; set; }
    }
    public class UberEstimate
    {
        public double lowEstimate { get; set; }

        public string displayName { get; set; }

        public double duration { get; set; }

        public double distance { get; set; }

        public double surgeMultiplier { get; set; }
        public double highEstimate { get; set; }

        public double midEstimate { get; set; }
    }
    public class Distance
    {
        public string text { get; set; }
        public int value { get; set; }
    }

    public class Duration
    {
        public string text { get; set; }
        public int value { get; set; }
    }

    public class Element
    {
        public Distance distance { get; set; }
        public Duration duration { get; set; }
        public string status { get; set; }
    }

    public class Row
    {
        public List<Element> elements { get; set; }
    }
    public class AddressComponent
    {
        public string long_name { get; set; }
        public string short_name { get; set; }
        public List<string> types { get; set; }
    }

    public class Location
    {
        public double lat { get; set; }
        public double lng { get; set; }
    }

    public class Northeast
    {
        public double lat { get; set; }
        public double lng { get; set; }
    }

    public class Southwest
    {
        public double lat { get; set; }
        public double lng { get; set; }
    }

    public class Viewport
    {
        public Northeast northeast { get; set; }
        public Southwest southwest { get; set; }
    }

    public class Geometry
    {
        public Location location { get; set; }
        public string location_type { get; set; }
        public Viewport viewport { get; set; }
    }

    public class Result
    {
        public List<AddressComponent> address_components { get; set; }
        public string formatted_address { get; set; }
        public Geometry geometry { get; set; }
        public string place_id { get; set; }
        public List<string> types { get; set; }
    }

    public class GeocodingPayload
    {
        public List<Result> results { get; set; }
        public string status { get; set; }
    }

    public class DistanceMatrixPayload
    {
        public List<string> destination_addresses { get; set; }
        public List<string> origin_addresses { get; set; }
        public List<Row> rows { get; set; }
        public string status { get; set; }
    }
    public class Price
    {
        public string localized_display_name { get; set; }
        public double high_estimate { get; set; }
        public string minimum { get; set; }
        public int duration { get; set; }
        public string estimate { get; set; }
        public double distance { get; set; }
        public string display_name { get; set; }
        public string product_id { get; set; }
        public double low_estimate { get; set; }
        public double surge_multiplier { get; set; }
        public string currency_code { get; set; }
    }

    public class UberRatePayload
    {
        public List<Price> prices { get; set; }
    }

    public class FareBreakup
    {
        public string type { get; set; }
        public double minimum_distance { get; set; }
        public double minimum_time { get; set; }
        public double base_fare { get; set; }
        public double minimum_fare { get; set; }
        public double cost_per_distance { get; set; }
        public double waiting_cost_per_minute { get; set; }
        public double ride_cost_per_minute { get; set; }
        public List<object> surcharge { get; set; }
        public double convenience_charge { get; set; }
        public string night_time_charges { get; set; }
        public string night_time_duration { get; set; }
    }

    public class Category
    {
        public string id { get; set; }
        public string display_name { get; set; }
        public string currency { get; set; }
        public string distance_unit { get; set; }
        public string time_unit { get; set; }
        public int eta { get; set; }
        public double distance { get; set; }
        public string image { get; set; }
        public List<FareBreakup> fare_breakup { get; set; }
    }

    public class RideEstimate
    {
        public string category { get; set; }
        public double distance { get; set; }
        public int travel_time_in_minutes { get; set; }
        public double amount_min { get; set; }
        public double amount_max { get; set; }

    }

    public class OlaRatePayload
    {
        public List<Category> categories { get; set; }
        public List<RideEstimate> ride_estimate { get; set; }
    }

    public class UberTimeEstimate
    {
        public string localized_display_name { get; set; }
        public int estimate { get; set; }
        public string display_name { get; set; }
        public string product_id { get; set; }
    }

    public class UberTimeEstimatePayload
    {
        public List<UberTimeEstimate> times { get; set; }
    }
}