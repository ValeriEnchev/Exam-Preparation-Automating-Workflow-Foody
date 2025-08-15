using FoodyTests.Models;
using RestSharp;
using RestSharp.Authenticators;
using System.Net;
using System.Text.Json;
using System.Threading.Channels;

namespace FoodyTests
{
    public class FoodyUnitTests
    {
        private RestClient client;
        private const string BaseUrl = "http://softuni-qa-loadbalancer-2137572849.eu-north-1.elb.amazonaws.com:86";

        private const string UserName = "valee70";
        private const string Password = "val123";

        private static Random random = new Random();
        private static string? lastCreatedFoodId;
        private const string nonExistedFoodId = "-1";

        [OneTimeSetUp]
        public void Setup()
        {
            string jwtToken = GetJwtToken(UserName, Password);
            var options = new RestClientOptions(BaseUrl)
            {
                Authenticator = new JwtAuthenticator(jwtToken),
            };
            client = new RestClient(options);
        }

        private string GetJwtToken(string userName, string password)
        {
            RestClient tmpClient = new RestClient(BaseUrl);
            RestRequest? request = new RestRequest("/api/User/Authentication", Method.Post);
            request.AddJsonBody(new { userName, password });
            RestResponse? response = tmpClient.Post(request);

            if (response.IsSuccessStatusCode)
            {
                JsonElement content = JsonSerializer.Deserialize<JsonElement>(response.Content);
                string token = content.GetProperty("accessToken").ToString();
                return token;
            }
            else
            {
                throw new InvalidOperationException($"Failed to authenticate. Status code: {response.StatusCode}, Content: {response.Content}");
            }
        }

        private static string GetRandomString(int length)
        {
            const string? chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            char[] stringChars = new char[length];

            for (int i = 0; i < length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            return new string(stringChars);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            client.Dispose();
        }

        //1.3. Create a New Food with the Required Fields
        [Test, Order(1)]
        public void Test1_CrateFood_ShouldReturnsCrated()
        {
            var newFood = new FoodDTO
            {
                Name = $"Food_{GetRandomString(6)}",
                Description = $"Description {GetRandomString(16)}",
                Url = ""
            };

            var request = new RestRequest("/api/Food/Create", Method.Post);
            request.AddJsonBody(newFood);
            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
           
            var jsonResponse = JsonSerializer.Deserialize<ApiResponseDTO>(response.Content);
            Assert.That(jsonResponse.FoodId, Is.Not.Null.And.Not.Empty, "Response does not contain a 'foodId' property.");
            lastCreatedFoodId = jsonResponse.FoodId;

            // Or from Ivailo Dimitrov:
            //var json = JsonSerializer.Deserialize<JsonElement>(response.Content);
            //lastCreatedFoodId = json.GetProperty("foodId").GetString() ?? string.Empty;
            //Assert.That(lastCreatedFoodId, Is.Not.Null.And.Not.Empty, "Food ID should not be null or empty.");

            //Console.WriteLine(lastCreatedFoodId);
        }

        //1.4. Edit the Title of the Food that you Created
        [Test, Order(2)]
        public void Test2_EditFoodTitle_ShouldReturnsOK()
        {
            //Console.WriteLine(lastCreatedFoodId);
            var changes = new[]
            {
                new { 
                    path = "/name", 
                    op = "replace", 
                    value = $"Updated_food_{GetRandomString(6)}"
                }
            };
            //var request = new RestRequest("/api/Food/Edit", Method.Patch);
            //request.AddQueryParameter("foodId", lastCreatedFoodId);      
            var request = new RestRequest($"/api/Food/Edit/{lastCreatedFoodId}", Method.Patch);
            
            request.AddJsonBody(changes);

            var response = client.Execute(request);

            var jsonResponse = JsonSerializer.Deserialize<ApiResponseDTO>(response.Content);
            
            //•	Assert that the response status code is OK(200).
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            //•	Assert that the response message indicates the idea was "Successfully edited".
            Assert.That(jsonResponse.Msg, Is.EqualTo("Successfully edited"));

        }

        // 1.5. Get All Foods
        [Test, Order(3)]
        public void Test3_GetAllFoods_ShoudShowNotEmpty()
        {
            var request = new RestRequest("/api/Food/All", Method.Get);
            var response = client.Execute(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            //var jsonResponse = JsonSerializer.Deserialize<List<ApiResponseDTO>>(response.Content);
            var jsonResponse = JsonSerializer.Deserialize<List<JsonElement>>(response.Content);
            Assert.That(jsonResponse, Is.InstanceOf<List<JsonElement>>());
            Assert.That(jsonResponse.Count, Is.GreaterThan(0));
            Assert.That(jsonResponse, Is.Not.Empty);
            
        }

        //1.6. Delete the Food that you Edited
        [Test, Order(4)]
        public void Test4_DeleteFood_ShouldDeleteEditedFood()
        {
            var request = new RestRequest($"/api/Food/Delete/{lastCreatedFoodId}", Method.Delete);

            var response = client.Execute(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var jsonResponse = JsonSerializer.Deserialize<ApiResponseDTO>(response.Content);
            Assert.That(jsonResponse.Msg, Is.EqualTo("Deleted successfully!"));
            
        }

        // 1.7. Try to Create a Food without the Required Fields
        [Test, Order(5)]
        public void Test5_CreateFood_WithoutRequiredFields_ShouldReturnBadRequest()
        {
            var newFood = new FoodDTO
            {
                Name = "",
                Description = "",
                Url = ""
            };

            var request = new RestRequest("/api/Food/Create", Method.Post);
            request.AddJsonBody(newFood);
            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            Assert.That(response.Content, Does.Contain("The Name field is required."));
            Assert.That(response.Content, Does.Contain("The Description field is required."));

        }

        // 1.8. Try to Edit a Non-existing Food
        [Test, Order(6)]
        public void Test6_EditNonExistingFood_ShouldReturnNotFound()
        {
            var changes = new[]
            {
                new {
                    path = "/name",
                    op = "replace",
                    value = $"Updated_food_{GetRandomString(6)}"
                }
            };
            //var request = new RestRequest("/api/Food/Edit", Method.Patch);
            //request.AddQueryParameter("foodId", lastCreatedFoodId);      
            var request = new RestRequest($"/api/Food/Edit/{nonExistedFoodId}", Method.Patch);

            request.AddJsonBody(changes);

            var response = client.Execute(request);

            var jsonResponse = JsonSerializer.Deserialize<ApiResponseDTO>(response.Content);

            //•	Assert that the response status code is OK(200).
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            //•	Assert that the response message indicates the idea was "No food revues...".
            Assert.That(jsonResponse.Msg, Is.EqualTo("No food revues..."));

        }

        // 1.9. Try to Delete a Non-existing Food
        [Test, Order(7)]
        public void Test7_DeleteNonExistingFood_ShouldReturnBadRequest()
        {
            var request = new RestRequest($"/api/Food/Delete/{nonExistedFoodId}", Method.Delete);

            var response = client.Execute(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            var jsonResponse = JsonSerializer.Deserialize<ApiResponseDTO>(response.Content);
            Assert.That(jsonResponse.Msg, Is.EqualTo("Unable to delete this food revue!"));

        }
    }
}
