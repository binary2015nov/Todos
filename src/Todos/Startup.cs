using System;
using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Funq;
using ServiceStack;
using ServiceStack.Configuration;
using ServiceStack.Host.Handlers;
using ServiceStack.Redis;

namespace Todos
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseUrls("http://localhost:55000/")
                .Build();
    }

    public class Startup
    {
        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseServiceStack(new AppHost());

            app.Use(new RequestInfoHandler());
        }
    }

    // Create your ServiceStack Web Service with a singleton AppHost
    public class AppHost : AppHostBase
    {
        // Initializes your AppHost Instance, with the Service Name and assembly containing the Services
        public AppHost() : base("Backbone.js TODO", typeof(TodoService).GetAssembly()) 
        { 
            AppSettings = new MultiAppSettings(
                new EnvironmentVariableSettings(),
                new AppSettings());
        }

        // Configure your AppHost with the necessary configuration and dependencies your App needs
        public override void Configure(Container container)
        {
            //Register Redis Client Manager singleton in ServiceStack's built-in Func IOC
            container.Register<IRedisClientsManager>(c =>
                new RedisManagerPool(AppSettings.Get("REDIS_HOST", defaultValue:"localhost")));
        }
    }

    // Define your ServiceStack web service request (i.e. Request DTO).
    [Route("/todos")]
    [Route("/todos/{Id}")]
    public class Todo
    {
        public long Id { get; set; }
        public string Content { get; set; }
        public int Order { get; set; }
        public bool Done { get; set; }
    }

    // Create your ServiceStack rest-ful web service implementation. 
    public class TodoService : Service
    {
        public object Get(Todo todo)
        {
            //Return a single Todo if the id is provided.
            if (todo.Id != default(long))
                return Redis.As<Todo>().GetById(todo.Id);

            //Return all Todos items.
            return Redis.As<Todo>().GetAll();
        }

        // Handles creating and updating the Todo items.
        public Todo Post(Todo todo)
        {
            var redis = Redis.As<Todo>();

            //Get next id for new todo
            if (todo.Id == default(long))
                todo.Id = redis.GetNextSequence();

            redis.Store(todo);

            return todo;
        }

        // Handles creating and updating the Todo items.
        public Todo Put(Todo todo)
        {
            return Post(todo);
        }

        // Handles Deleting the Todo item
        public void Delete(Todo todo)
        {
            Redis.As<Todo>().DeleteById(todo.Id);
        }
    }
}
