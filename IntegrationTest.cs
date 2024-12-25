using FlightManagementSystem.Infrastructure;
using FlightManagementSystem.Models;
using FlightManagementSystem.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;
using RabbitMQ.Client;
using System.Text;

namespace FlightManagementSystemTests
{

    [TestClass]
    public class RabbitMqServiceItTests
    {
        //Pre-Requisites:
        //RabbitMQ server must be running locally or accessible at localhost:5672 with guest credentials.

        [TestMethod]
        public async Task PublishAndConsumeMessage_ShouldMatchPublishedMessage()
        {
            // Arrange: Create RabbitMQ factory and service
            var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<RabbitMqService>();
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest",
                Port = 5672,
                VirtualHost = "/"
            };
            var rabbitMqService = new RabbitMqService(logger, factory);

            var flightMessage = new FlightNotification
            {
                FlightId = "111",
                Airline = "AirlineName",
                Origin = "OriginCity",
                Destination = "DestinationCity",
                Price = 500,
                Currency = "USD",
                DepartureDate = DateTime.Now
            };

            // Act: Publish the message
            await rabbitMqService.PublishMessageAsync(flightMessage);

            // Act: Consume the message
            FlightNotification? consumedMessage = null;
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(10)); // Timeout to avoid infinite waiting

            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                var message = await rabbitMqService.ConsumeMessageAsync(cancellationTokenSource.Token);
                if (message != null)
                {
                    consumedMessage = message;
                    break;
                }

                await Task.Delay(100); // Avoid busy waiting
            }

            // Assert: Verify the consumed message matches the published message
            Assert.IsNotNull(consumedMessage, "No message was consumed from the queue.");
            Assert.AreEqual(flightMessage.FlightId, consumedMessage.FlightId);
            Assert.AreEqual(flightMessage.Airline, consumedMessage.Airline);
            Assert.AreEqual(flightMessage.Origin, consumedMessage.Origin);
            Assert.AreEqual(flightMessage.Destination, consumedMessage.Destination);
            Assert.AreEqual(flightMessage.Price, consumedMessage.Price);
            Assert.AreEqual(flightMessage.Currency, consumedMessage.Currency);
            Assert.AreEqual(flightMessage.DepartureDate.ToString("o"), consumedMessage.DepartureDate.ToString("o"));
        }

    }

    [TestClass]
    public class UserRepositoryItTests
    {
        private IMongoClient? _mongoClient;
        private UserRepository? _repository;

        [TestInitialize]
        public void Setup()
        {
            // Prerequisite: Make sure MongoDB is running locally or in Docker
            _mongoClient = new MongoClient("mongodb://localhost:27017");
            _repository = new UserRepository(_mongoClient);
        }

        [TestMethod]
        public async Task AddUserAsync_ReadAndDelete_ShouldWorkCorrectly()
        {
            // Arrange
            var user = new User { Name = "Test User", Email = "test@example.com" };

            // Act: Add user to the database
            var addedUser = await _repository!.AddUserAsync(user);

            // Assert: Verify user was added correctly
            Assert.AreEqual(user.Name, addedUser.Name);
            Assert.AreEqual(user.Email, addedUser.Email);

            // Act: Read the user by ID
            var retrievedUser = await _repository.GetUserByIdAsync(addedUser.Id!);

            // Assert: Verify retrieved user matches the added user
            Assert.IsNotNull(retrievedUser);
            Assert.AreEqual(addedUser.Id, retrievedUser.Id);
            Assert.AreEqual(addedUser.Name, retrievedUser.Name);
            Assert.AreEqual(addedUser.Email, retrievedUser.Email);

            // Act: Delete the user
            User? deleteResult = await _repository.DeleteUserAsync(addedUser.Id!);

            // Assert: Verify deletion was successful
            Assert.IsTrue(deleteResult != null);

            // Verify the user no longer exists in the database
            var deletedUser = await _repository.GetUserByIdAsync(addedUser.Id!);
            Assert.IsNull(deletedUser);
        }
    }

    [TestClass]
    public class PushNotificationServiceItTests
    {
        [TestMethod]
        public void SendPushAlert_ShouldLogMessage()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<PushNotificationService>>();
            var service = new PushNotificationService(loggerMock.Object);

            var user = new User { Name = "John Doe", Email = "johnDoe@gmail.com" };

            var flightMessage = new FlightNotification
            {
                FlightId = "111",
                Airline = "AirlineName",
                Origin = "OriginCity",
                Destination = "DestinationCity",
                Price = 500,
                Currency = "USD",
                DepartureDate = DateTime.Now
            };

            var notificationMessage = $"Hi {user.Name}, the flight '{flightMessage.Airline}' from {flightMessage.Origin} to {flightMessage.Destination} is now available for {flightMessage.Price} {flightMessage.Currency}.";

            // Act
            service.SendPushAlert(notificationMessage);

            // Assert
            loggerMock.Verify(
                    x => x.Log(
                        LogLevel.Information,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((state, t) => state.ToString()!.Contains($"Push alert sent: {notificationMessage}")),
                        It.IsAny<Exception?>(),
                        It.Is<Func<It.IsAnyType, Exception?, string>>((state, exception) => true)), // Accept any valid formatter
                    Times.Once
                );

        }
    }

    [TestClass]
    public class UserServiceItTests
    {
        private IUserService _userService;
        private IUserRepository _userRepository;

        [TestInitialize]
        public void TestInitialize()
        {
            // Replace the connection string and database name with your environment details
            _userRepository = new UserRepository(new MongoClient("mongodb://localhost:27017"));
            _userService = new UserService(_userRepository);
        }

        [TestMethod]
        public async Task UserAlertPreferencesIntegrationTest()
        {
            // Step 1: Create a user
            var user = new User
            {
                Name = "Test User 123",
                Email = "test123@example.com",
                MobileDeviceToken = "TestDeviceToken123"
            };
            var createdUser = await _userService.AddUserAsync(user);
            Assert.IsNotNull(createdUser);
            Assert.AreEqual(user.Name, createdUser.Name);

            // Step 2: Define and add two UserAlertPreferences for the user
            var alertPreference1 = new UserAlertPreference
            {
                PreferenceId = Guid.NewGuid().ToString(),
                Destination = "New York",
                MaxPrice = 500,
                Currency = "USD"
            };
            var alertPreference2 = new UserAlertPreference
            {
                PreferenceId = Guid.NewGuid().ToString(),
                Destination = "Paris",
                MaxPrice = 600,
                Currency = "EUR"
            };

            var userWithPreference1 = await _userService.AddAlertPreferenceAsync(createdUser.Id!, alertPreference1);
            Assert.IsNotNull(userWithPreference1);
            Assert.AreEqual(1, userWithPreference1.AlertPreferences.Count);

            var userWithPreference2 = await _userService.AddAlertPreferenceAsync(createdUser.Id!, alertPreference2);
            Assert.IsNotNull(userWithPreference2);
            Assert.AreEqual(2, userWithPreference2.AlertPreferences.Count);

            // Step 3: Read the user's preferences
            var userWithPreferences = await _userService.GetUserByIdAsync(createdUser.Id!);
            Assert.IsNotNull(userWithPreferences.AlertPreferences);
            Assert.AreEqual(2, userWithPreferences.AlertPreferences.Count);

            // Step 4: Update one of the UserAlertPreferences
            var updatedPreference = new UserAlertPreference
            {
                PreferenceId = alertPreference1.PreferenceId,
                Destination = alertPreference1.Destination,
                MaxPrice = 450, // Adjust the MaxPrice
                Currency = alertPreference1.Currency
            };
            var updatedUser = await _userService.UpdateAlertPreferenceAsync(createdUser.Id!, alertPreference1.PreferenceId!, updatedPreference);
            Assert.IsNotNull(updatedUser);

            var updatedAlertPreference = updatedUser.AlertPreferences.First(p => p.PreferenceId == alertPreference1.PreferenceId);
            Assert.AreEqual(450, updatedAlertPreference.MaxPrice);

            // Step 5: Remove both UserAlertPreferences
            var userAfterFirstRemoval = await _userService.DeleteAlertPreferenceAsync(createdUser.Id!, alertPreference1.PreferenceId!);
            Assert.IsNotNull(userAfterFirstRemoval);
            Assert.AreEqual(1, userAfterFirstRemoval.AlertPreferences.Count);

            var userAfterSecondRemoval = await _userService.DeleteAlertPreferenceAsync(createdUser.Id!, alertPreference2.PreferenceId!);
            Assert.IsNotNull(userAfterSecondRemoval);
            Assert.AreEqual(0, userAfterSecondRemoval.AlertPreferences.Count);

            await _userService.DeleteUserAsync(createdUser.Id!);
        }

    }

    //[TestClass]
    //public class FlightPriceServiceItTests
    //{
    //    [TestMethod]
    //    public async Task FetchFlightPricesAsync_ShouldLogFetching()
    //    {
    //        // Arrange
    //        var loggerMock = new Mock<ILogger<FlightPriceService>>();
    //        //var configMock = null;// new Mock<IConfiguration>();
    //        var rabbitMqServiceMock = new Mock<IRabbitMqService>();

    //        var service = new FlightPriceService(loggerMock.Object, /*configMock.Object*/null, null, rabbitMqServiceMock.Object);
    //        var cancellationToken = CancellationToken.None;

    //        // Act
    //        await service.FetchFlightPricesAsync(cancellationToken);

    //        // Assert
    //        loggerMock.Verify(l => l.LogInformation(It.IsAny<string>()), Times.AtLeastOnce);
    //    }
    //}

    //[TestClass]
    //public class NotificationServiceItTests
    //{
    //    [TestMethod]
    //    public async Task ProcessNotification_ShouldSendAlerts()
    //    {
    //        // Arrange
    //        var userServiceMock = new Mock<IUserService>();
    //        var rabbitMqServiceMock = new Mock<IRabbitMqService>();
    //        var pushNotificationServiceMock = new Mock<IPushNotificationService>();

    //        var service = new NotificationService(userServiceMock.Object, rabbitMqServiceMock.Object, pushNotificationServiceMock.Object);

    //        var cancellationToken = CancellationToken.None;

    //        // Act
    //        await service.StartAsync(cancellationToken);

    //        // Assert
    //        pushNotificationServiceMock.Verify(p => p.SendPushAlert(It.IsAny<string>()), Times.AtLeastOnce);
    //    }
    //}

}
