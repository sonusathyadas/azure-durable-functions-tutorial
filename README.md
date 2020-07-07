# Azure Durable Functions Tutorial

## Order Processing Workflow

![Order processing workflow](images/workflow.png "Order processing workflow")

This tutorials demonstrates how to create a Azure Durable functions to create an order processing workflow for confirming the order details placed by an e-commerce application and send confirmation or cancellation mail to the customer according to the status of the payment. Orchestrator function calls the `CheckPaymentStatus` activity function to check the payment status. If payment status is `Completed`, the orchestrator function calls a `SendOrderToVendorQueue` to send the order details to the vendors queue in a storage account. After the order details are sent to the `vendor-orders` queue the workflow executes a `SendConfirmationMail` activity function to send an order confirmation mail to the customer. If the payment status is not `Completed` the `SendCancellationMail` function will be executed and it send a mail to the customer indicating the cancellation of order due to payment failure. 

In this tutorial we are using `Azure SQL database`, `SendGrid` and `Storage account Queue` services for the execution of the workflow.

### Prerequisites
*) Azure Subscription
*) .NET Core 3.1 SDK
*) Visual Studio 2019 (Azure Development must be enabled during installation)
*) Postman (REST api testing tool)
*) SQL Server Management Studio 

## Preparing the Azure Services
### Create and configure Azure SQL Database
1) Open Azure Portal by navigating to [https://portal.azure.com](https://portal.azure.com) and login to the Azure account.
2) Create a new resource group with the name `AzureFunctionsGroup`.
3) Create an `SQL Database` service instance by selecting `Create a resource > Databases > SQL Database`. 
4) Provide the following values to create the database and logical server.
        
    |Parameter          | Value                       |
    |-------------------|-----------------------------|
    |Basic              |                             |
    |Resource Group     | AzureFunctionsGroup         |
    |Location           | East US                     |
    |Database name      | eshopdb                     |
    |Server Name        | eshop-server                |
    |Admin user         | labuser                     |
    |Password           | Password@123                |
    |Compute + Stroage  | General Purpose (Serverless)|
    |Networking         |                             |
    |Connectivity Method| Public endpoint             |
    |Allow Azure services and resources to access this server              | Yes                         |
    |Add current client IP address| Yes               |
    |Additional Settings                              |
    |Use existing data  | None                        |
    |Collation          | Do not modify. Leave the default value|

5) After the database server is created, go to the overview page and copy the `Server name` value. Open `SQL Server Management Studio` and connect using the Server name you have copied and the admin user name and password which you have used at the time of creating service.
6) Open a new query window in `SQL Server Management Studio` and run the following query to create the payments table.
    ```
    USE eshopdb
    GO
    
    CREATE TABLE [dbo].[Payments](
    	[Id] [int] IDENTITY(1,1) NOT NULL,
    	[OrderId] [int] NOT NULL,
    	[PaymentMode] [varchar](25) NOT NULL,
    	[Amount] [numeric](10, 2) NOT NULL,
    	[PaymentStatus] [varchar](20) NOT NULL,
     CONSTRAINT [PK_Payments] PRIMARY KEY CLUSTERED 
    (
    	[Id] ASC
    )WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY]
    GO    
    ``` 
7) After the `Payments` table is created run the following queries to insert some sample records in to the table.
    ```
    INSERT INTO [dbo].[Payments] VALUES ( 101, 'Cash', 5000, 'Completed')
    INSERT INTO [dbo].[Payments] VALUES ( 102, 'Cash', 12300, 'Completed')
    INSERT INTO [dbo].[Payments] VALUES ( 103, 'CC', 7600, 'Pending')
    INSERT INTO [dbo].[Payments] VALUES ( 104, 'DC', 3200, 'Completed')
    INSERT INTO [dbo].[Payments] VALUES ( 105, 'Online', 4300, 'Pending')
    GO
    ```
8) Go to Azure Portal and copy the database connection string from the `Connection Strings` section of your database service and save into a text file. We will be using this while creating the Durable function. Replace `{your_password}` with the password value you have used while creating the Database instance.

### Create and configure the storage account queue
1) Open Azure Portal and create a new storge account service by choosing `Create a resource > Storage > Storage account`.
2) Provide the following values to create the storage account.
    
    |Parameter              |Value                       |
    |-----------------------|----------------------------|
    |Resource group         | AzureFunctionsGroup        |
    |Storage account name   | eshopstorage               |
    |Location               | East US                    |
    |Performance            | Standard                   |
    |Account Kind           | Storage V2                 |
    |Replication            | LRS                        |
    |Access Tier            | Hot                        |
    |Connectivity Method    | Public endpoint            |
    
3) Once the storage account is created, open the storage account and navigate to the Queues section. 
4) Click on `+ Queue` to create a new queue. Provide the queue name as `vendor-orders` and click OK.

### Create and configure SendGrid account
1) Open Azure Portal and click on `Create a resource`.
2) Search for `SendGrid` and click on `Create` to create a new `SendGrid` service.
3) Provide the following values to create the account
    |Parameter              |Value                       |
    |-----------------------|----------------------------|
    |Resource group         | AzureFunctionsGroup        |    
    |Location               | East US                    |
    |Name                   | EshopSendgrid              |
    |Password               | Password@123               |
    |Pricing Tier           | Free                       |
    |First name             | &lt;Your first name&gt;    |
    |Last name              | &lt;Your last name&gt;     |
    |Email                  | &lt;Your email id&gt;      |
    |Company                | &lt;Your company name&gt;  |
    |Website                | &lt;Your company website&gt;|

4) Once the SendGrid account is created, Click on the `Manage` button from the overview page. This will open the SendGrid account console page in new tab.  
5) On the left pane, select `Settings` and click on the `API Keys` to create a new API key for your application.
6) Click in the `Create API Key` button and provide a name for the API key. Choose `Full Access` from the `API Key Permissions` list and click on `Create and View`.
7) Copy the API key in to a text file which we need to configure in out Durable functions application.

### Create Azure Functions App 
1) Open Azure Portal and click on `create a resource ` and search for `Function App`. From the list choose `Function App` and click Create.
2) Provide the following details to create Function App:
    |Parameter              |Value                       |
    |-----------------------|----------------------------|
    |Resource group         | AzureFunctionsGroup        |    
    |Region                 | East US                    |
    |Name                   | eshoporderprocesssor       |
    |Publish                | Code                       |
    |Runtime stack          | .NET Core                  |
    |Version                | 3.1                        |
    |Hosting                |                            |
    |Storage account        | Select the storage account created above |
    |Operating System       | Windows                    |
    |Plan                   | Consumption Plan           |

3) Click on create to create the Function App.
