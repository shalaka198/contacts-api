variable "aws_region" {
  description = "AWS region to deploy into."
  type        = string
  default     = "ap-southeast-2"
}

variable "environment" {
  description = "Deployment environment: staging | production."
  type        = string
  validation {
    condition     = contains(["staging", "production"], var.environment)
    error_message = "environment must be 'staging' or 'production'."
  }
}

variable "app_name" {
  description = "Application name used to prefix all resource names."
  type        = string
  default     = "contacts-api"
}

variable "vpc_cidr" {
  description = "CIDR block for the VPC."
  type        = string
  default     = "10.0.0.0/16"
}

variable "public_subnet_cidrs" {
  description = "CIDR blocks for public subnets (one per AZ)."
  type        = list(string)
  default     = ["10.0.1.0/24", "10.0.2.0/24"]
}

variable "private_subnet_cidrs" {
  description = "CIDR blocks for private subnets (one per AZ)."
  type        = list(string)
  default     = ["10.0.11.0/24", "10.0.12.0/24"]
}

variable "availability_zones" {
  description = "AZs to spread resources across."
  type        = list(string)
  default     = ["ap-southeast-2a", "ap-southeast-2b"]
}

variable "api_image_tag" {
  description = "Docker image tag to deploy (e.g. git SHA or semver tag)."
  type        = string
  default     = "latest"
}

variable "api_desired_count" {
  description = "Desired number of ECS Fargate tasks."
  type        = number
  default     = 2
}

variable "api_cpu" {
  description = "CPU units for each Fargate task (1024 = 1 vCPU)."
  type        = number
  default     = 512
}

variable "api_memory" {
  description = "Memory in MiB for each Fargate task."
  type        = number
  default     = 1024
}

variable "db_instance_class" {
  description = "RDS Aurora PostgreSQL instance class."
  type        = string
  default     = "db.t4g.medium"
}

variable "db_name" {
  description = "PostgreSQL database name."
  type        = string
  default     = "contacts"
}

variable "jwt_secret" {
  description = "JWT signing secret. Stored in AWS Secrets Manager."
  type        = string
  sensitive   = true
}
