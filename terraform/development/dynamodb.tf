resource "aws_dynamodb_table" "processesapi_dynamodb_table" {
  name           = "Processes"
  billing_mode   = "PROVISIONED"
  read_capacity  = 10
  write_capacity = 10
  hash_key       = "id"

  attribute {
    name = "id"
    type = "S"
  }

  attribute {
    name = "targetId"
    type = "S"
  }

  global_secondary_index {
    name               = "ProcessesByTargetId"
    hash_key           = "targetId"
    range_key          = "id"
    write_capacity     = 10
    read_capacity      = 10
    projection_type    = "ALL"
  }

  tags = merge(
    local.default_tags,
    { BackupPolicy = "Dev" }
  )

  point_in_time_recovery {
    enabled = true
  }
}

