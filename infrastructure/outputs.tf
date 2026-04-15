output "ecr_repository_url" {
  description = "URL of the ECR repository to push images to."
  value       = aws_ecr_repository.api.repository_url
}

output "alb_dns_name" {
  description = "DNS name of the Application Load Balancer."
  value       = aws_lb.api.dns_name
}

output "ecs_cluster_name" {
  description = "Name of the ECS cluster."
  value       = aws_ecs_cluster.main.name
}

output "ecs_service_name" {
  description = "Name of the ECS service."
  value       = aws_ecs_service.api.name
}

output "rds_cluster_endpoint" {
  description = "Writer endpoint of the Aurora cluster."
  value       = aws_rds_cluster.main.endpoint
  sensitive   = true
}

output "cloudwatch_log_group" {
  description = "CloudWatch log group for ECS task logs."
  value       = aws_cloudwatch_log_group.api.name
}
