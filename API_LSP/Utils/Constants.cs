namespace API_LSP.Utils
{
    public static class Constants
    {
        // API Response Messages
        public static class Messages
        {
            public const string SUCCESS = "Success";
            public const string ERROR = "Error";
            public const string ALERT = "Alert";
            public const string DUPLICATE = "Duplicate Record";
            public const string VALIDATION_ERROR = "Validation Error";
            public const string NOT_FOUND = "Record Not Found";
            public const string UNAUTHORIZED = "Unauthorized Access";
            public const string INTERNAL_ERROR = "Internal Server Error";
        }

        // API Response Details
        public static class Details
        {
            public const string OPERATION_SUCCESS = "Operation completed successfully";
            public const string OPERATION_FAILED = "Operation failed";
            public const string RECORD_CREATED = "Record created successfully";
            public const string RECORD_UPDATED = "Record updated successfully";
            public const string RECORD_DELETED = "Record deleted successfully";
            public const string RECORD_NOT_FOUND = "The requested record was not found";
            public const string DUPLICATE_RECORD =
                "A record with the same information already exists";
            public const string INVALID_INPUT = "Please check your input data";
            public const string SERVER_ERROR = "An unexpected error occurred on the server";
        }

        // Database Constants
        public static class Database
        {
            public const int DEFAULT_PAGE_SIZE = 10;
            public const int MAX_PAGE_SIZE = 100;
            public const int COMMAND_TIMEOUT = 30;
        }

        // Status Codes
        public static class StatusCodes
        {
            public const int SUCCESS = 200;
            public const int CREATED = 201;
            public const int BAD_REQUEST = 400;
            public const int UNAUTHORIZED = 401;
            public const int NOT_FOUND = 404;
            public const int CONFLICT = 409;
            public const int INTERNAL_ERROR = 500;
        }

        public const int ROAD_TEST = 1;
        public const int UNDERBODY = 2;
        public const int DTC = 3;
        public const int SHOWER_TEST = 4;
        public const int ECOS = 5;
        public const int LSP_AUDIT = 6;
        public const int VMS = 7;
        public const int RECHECK = 8;
        public const int REWORK = 9;
        public const int PHYSICAL_RFD = 11;

    }

}
