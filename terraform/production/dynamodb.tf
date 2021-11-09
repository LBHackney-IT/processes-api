resource "aws_dynamodb_table" "processesapi_dynamodb_table" {
  name           = "Processes"
  billing_mode   = "PROVISIONED"
  read_capacity  = 10
  write_capacity = 10
  hash_key       = "id"
  range_key       = "processName"

  attribute {
    name = "id"
    type = "S"
  }

  attribute {
    name = "processName"
    type = "S"
  }
  
  tags = merge(
    local.default_tags,
    { BackupPolicy = "Prod" }
  )

  point_in_time_recovery {
    enabled = true
  }
}

