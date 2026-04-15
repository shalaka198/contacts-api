terraform {
  required_version = ">= 1.7"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  # Remote state — store in S3 + DynamoDB lock table
  backend "s3" {
    bucket         = "contacts-api-tf-state"
    key            = "contacts/terraform.tfstate"
    region         = "ap-southeast-2"
    encrypt        = true
    dynamodb_table = "contacts-api-tf-lock"
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = "contacts-api"
      ManagedBy   = "terraform"
      Environment = var.environment
    }
  }
}
