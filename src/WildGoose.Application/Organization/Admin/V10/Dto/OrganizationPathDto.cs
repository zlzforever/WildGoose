﻿namespace WildGoose.Application.Organization.Admin.V10.Dto
{
    public class OrganizationPathDto : OrganizationSimpleDto
    {
        /// <summary>
        /// Id Path
        /// </summary>
        public List<string> Path { get; set; }
        public string FullName { get; set; }
    }
}
